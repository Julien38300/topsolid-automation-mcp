#!/usr/bin/env python3
"""
LoRA training for TopSolid 3B recipe sub-agent.

Full pipeline: train → save adapters → merge → convert GGUF → quantize Q4_K_M
Handles the known Unsloth save_pretrained_merged bug by using PEFT merge + llama.cpp.

Usage (from WSL2):
  source ~/lora-env/bin/activate
  cd /mnt/c/path/to/topsolid-automation-mcp
  python scripts/train-lora.py

Output:
  ~/topsolid_lora_en/          — LoRA adapter (for resume/inspection)
  ~/topsolid_merged_en/        — Merged 16-bit HuggingFace model
  ~/ministral-topsolid-en.gguf — Q4_K_M GGUF ready for Ollama
"""

import json
import os
import sys
import shutil
import subprocess
from pathlib import Path

# ============================================================================
# CONFIGURATION
# ============================================================================

MODEL_NAME = "unsloth/Ministral-3-3B-Instruct-2512-bnb-4bit"

# Training sequence length. Override with LORA_MAX_SEQ_LENGTH.
# 4096 = num_ctx servi par skills/topsolid-mcp/Modelfile (PARAMETER num_ctx
# 4096) : entraîner à la longueur réellement servie (T-1066 / étape 8).
MAX_SEQ_LENGTH = int(os.environ.get("LORA_MAX_SEQ_LENGTH", "4096"))
LORA_R = 32
LORA_ALPHA = 64
EPOCHS = 3
BATCH_SIZE = 2
GRAD_ACCUM = 4
LR = 2e-4

DATASET_FILE = os.environ.get(
    "LORA_DATASET",
    str(Path(__file__).parent.parent / "data" / "lora-dataset.jsonl")
)

# T-1066 / étape 7 : the [AVAILABLE_TOOLS] block must be rendered exactly the
# way the Modelfile TEMPLATE injects `$.Tools` at serving time. This file is
# captured from the REAL compiled server (scripts/capture-available-tools.py,
# MCP -> Ollama function format, compact JSON). Empty/missing -> samples are
# rendered WITHOUT tools (eval numbers then only cover the no-tools regime).
TOOLS_FILE = os.environ.get(
    "LORA_TOOLS_FILE",
    str(Path(__file__).parent.parent / "data" / "available-tools.json")
)


def load_tools_blob():
    """Serialise la liste d'outils servie exactement comme `$.Tools`."""
    p = Path(TOOLS_FILE)
    if not p.is_file():
        return ""
    try:
        json.loads(p.read_text(encoding="utf-8"))
    except Exception as e:
        raise SystemExit(f"Invalid TOOLS_FILE {p}: {e}") from e
    # Re-serialise compactément (le JSON de sortie de capture EST déjà compact ;
    # on repasse par json.dumps pour garantir la forme [AVAILABLE_TOOLS]{...}]).
    return json.dumps(json.loads(p.read_text(encoding="utf-8")),
                      ensure_ascii=False, separators=(",", ":"))


def _render_text(conv, tools_blob):
    """Un échantillon : SYSTEM [AVAILABLE_TOOLS] ... [INST] ... [/INST] ... </s>.

    Le TEMPLATE Ollama injecte le bloc d'outils juste AVANT le [INST] du
    dernier message utilisateur : pour les échantillons mono-tours générés par
    le pipeline (system, human, gpt), cela revient à l'insérer après le bloc
    système. C'est exactement ce que le TEMPLATE produit au service.
    """
    last_human = max((i for i, m in enumerate(conv)
                      if m.get("from") == "human"), default=-1)
    parts = []
    for i, msg in enumerate(conv):
        role = msg["from"]
        value = msg["value"]
        if role == "system":
            parts.append(f"[SYSTEM_PROMPT]{value}[/SYSTEM_PROMPT]")
        elif role == "human":
            if i == last_human and tools_blob:
                parts.append(f"[AVAILABLE_TOOLS]{tools_blob}[/AVAILABLE_TOOLS]")
            parts.append(f"[INST]{value}[/INST]")
        elif role == "gpt":
            # Assistant content. If it's a tool_call, no trailing </s> separator
            # here — Mistral native expects [TOOL_CALLS]...[ARGS]...</s> as a unit.
            parts.append(f"{value}</s>")
        elif role == "tool":
            parts.append(f"[TOOL_RESULTS]{value}[/TOOL_RESULTS]")
    return "".join(parts)

HOME = Path.home()
ADAPTER_DIR = HOME / "topsolid_lora_en"
MERGED_DIR = HOME / "topsolid_merged_en"
F16_GGUF = HOME / "ministral-topsolid-en-f16.gguf"
Q4_GGUF = HOME / "ministral-topsolid-en.gguf"
LLAMA_CPP = HOME / "llama.cpp"

# ============================================================================
# STEP 1: TRAINING
# ============================================================================

def step_train():
    """Train LoRA adapters with Unsloth."""
    import torch
    from unsloth import FastLanguageModel, is_bfloat16_supported
    from datasets import load_dataset
    from trl import SFTTrainer
    from transformers import TrainingArguments

    print(f"[TRAIN] Loading model: {MODEL_NAME}")
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME,
        max_seq_length=MAX_SEQ_LENGTH,
        dtype=None,
        load_in_4bit=True,
    )

    print(f"[TRAIN] Adding LoRA adapters (r={LORA_R}, alpha={LORA_ALPHA})")
    model = FastLanguageModel.get_peft_model(
        model,
        r=LORA_R,
        target_modules=["q_proj", "k_proj", "v_proj", "o_proj",
                         "gate_proj", "up_proj", "down_proj"],
        lora_alpha=LORA_ALPHA,
        lora_dropout=0,
        bias="none",
        use_gradient_checkpointing="unsloth",
        random_state=42,
    )

    # Mistral [INST] chat template — must match Ollama Modelfile TEMPLATE
    # Handles multi-turn conversations (system/human/gpt/tool roles).
    def formatting_prompts_func(examples):
        # TODO(train/serve skew) -- the samples built here look like:
        #   [SYSTEM_PROMPT]...[/SYSTEM_PROMPT][INST]question[/INST][TOOL_CALLS]name[ARGS]{...}</s>
        # and NEVER contain an [AVAILABLE_TOOLS] block. In production the Ollama
        # TEMPLATE (skills/topsolid-mcp/Modelfile) injects one right before the last
        # [INST] as soon as the client advertises tools: several thousand tokens of
        # T-1066 / étape 7 : le bloc [AVAILABLE_TOOLS] réel est désormais rendu
        # dans chaque échantillon via _render_text() — même payload que `$.Tools`
        # du TEMPLATE (capturé du serveur compilé par capture-available-tools.py,
        # sérialisation compacte identique). Override possible via LORA_TOOLS_FILE,
        # fichier absent -> échantillons sans outils (régime "no tools").
        convs = examples["conversations"]
        tools_blob = load_tools_blob()
        return {"text": [_render_text(conv, tools_blob) for conv in convs]}

    print(f"[TRAIN] Loading dataset: {DATASET_FILE}")
    dataset = load_dataset("json", data_files={"train": DATASET_FILE}, split="train")
    dataset = dataset.map(formatting_prompts_func, batched=True)
    print(f"[TRAIN] Dataset: {len(dataset)} samples, {EPOCHS} epochs")

    trainer = SFTTrainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=dataset,
        dataset_text_field="text",
        max_seq_length=MAX_SEQ_LENGTH,
        dataset_num_proc=2,
        packing=False,
        args=TrainingArguments(
            per_device_train_batch_size=BATCH_SIZE,
            gradient_accumulation_steps=GRAD_ACCUM,
            warmup_steps=5,
            max_steps=-1,
            num_train_epochs=EPOCHS,
            learning_rate=LR,
            fp16=not is_bfloat16_supported(),
            bf16=is_bfloat16_supported(),
            logging_steps=1,
            optim="adamw_8bit",
            weight_decay=0.01,
            lr_scheduler_type="linear",
            seed=42,
            output_dir="outputs",
        ),
    )

    stats = trainer.train()
    loss = stats.training_loss
    print(f"[TRAIN] Done! Loss: {loss:.4f}, Runtime: {stats.metrics['train_runtime']:.0f}s")

    # Save LoRA adapter
    model.save_pretrained(str(ADAPTER_DIR))
    tokenizer.save_pretrained(str(ADAPTER_DIR))
    print(f"[TRAIN] Adapter saved to {ADAPTER_DIR}")

    return model, tokenizer


# ============================================================================
# STEP 2: MERGE (workaround for Unsloth save_pretrained_merged bug)
# ============================================================================

def step_merge(model=None, tokenizer=None):
    """Merge LoRA into base model in fp16 precision.

    IMPORTANT: Unsloth's save_pretrained_merged crashes with Ministral-3B
    ('# of LoRAs = 350 does not match # of saved modules = 0').
    Workaround: reload base model in fp16 (NOT 4-bit), apply LoRA via PEFT,
    then merge_and_unload. This produces a clean fp16 model for llama.cpp.
    """
    import torch
    from peft import PeftModel

    print("[MERGE] Loading base model in fp16 (NOT 4-bit) for clean merge...")
    from unsloth import FastLanguageModel
    base_model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=MODEL_NAME,
        max_seq_length=MAX_SEQ_LENGTH,
        dtype=torch.float16,
        load_in_4bit=False,
    )

    print(f"[MERGE] Applying LoRA adapter from {ADAPTER_DIR}...")
    model = PeftModel.from_pretrained(base_model, str(ADAPTER_DIR))

    print("[MERGE] Merging LoRA weights into base model...")
    model = model.merge_and_unload()

    # Clean output dir
    if MERGED_DIR.exists():
        shutil.rmtree(MERGED_DIR)

    print(f"[MERGE] Saving merged fp16 model to {MERGED_DIR}...")
    model.save_pretrained(str(MERGED_DIR), safe_serialization=True)
    tokenizer.save_pretrained(str(MERGED_DIR))
    print(f"[MERGE] Saved ({sum(f.stat().st_size for f in MERGED_DIR.glob('*.safetensors')) / 1e9:.1f} GB)")

    # Fix tensor names: Ministral-3B is multimodal, PEFT saves with
    # "model.language_model.model.X" prefix instead of standard "model.X".
    # llama.cpp's convert_hf_to_gguf.py expects the standard naming.
    fix_tensor_names(MERGED_DIR)


def fix_tensor_names(model_dir):
    """Fix tensor name prefixes for llama.cpp compatibility.

    Ministral-3B-Instruct (Unsloth variant) uses a multimodal architecture
    with language_model wrapper. After PEFT merge, tensors are saved as:
      model.language_model.model.layers.X → must become model.layers.X
    Also extracts text_config from the multimodal config.json.
    """
    from safetensors.torch import load_file, save_file

    safetensor_files = list(model_dir.glob("*.safetensors"))
    if not safetensor_files:
        print("[MERGE] No safetensors files found, skipping tensor fix")
        return

    needs_fix = False
    for sf in safetensor_files:
        tensors = load_file(str(sf))
        if any(k.startswith("model.language_model.") for k in tensors.keys()):
            needs_fix = True
            break
        break

    if not needs_fix:
        print("[MERGE] Tensor names already correct, no fix needed")
        return

    print("[MERGE] Fixing tensor name prefixes for llama.cpp...")
    for sf in safetensor_files:
        tensors = load_file(str(sf))
        fixed = {}
        for k, v in tensors.items():
            new_k = k.replace("model.language_model.model.", "model.")
            new_k = new_k.replace("model.language_model.lm_head.", "lm_head.")
            fixed[new_k] = v
        save_file(fixed, str(sf))

    # Fix config.json: extract text_config if multimodal
    import json as _json
    config_path = model_dir / "config.json"
    if config_path.exists():
        with open(config_path) as f:
            config = _json.load(f)
        if "text_config" in config:
            text_config = config["text_config"]
            text_config["architectures"] = ["MistralForCausalLM"]
            text_config["model_type"] = "mistral"
            with open(config_path, "w") as f:
                _json.dump(text_config, f, indent=2)
            print("[MERGE] Extracted text_config from multimodal config")

    print("[MERGE] Tensor names fixed!")


# ============================================================================
# STEP 3: CONVERT TO GGUF (via llama.cpp — reliable, no Unsloth bugs)
# ============================================================================

def step_convert_gguf():
    """Convert HF model to GGUF F16, then quantize to Q4_K_M."""
    convert_script = LLAMA_CPP / "convert_hf_to_gguf.py"
    quantize_bin = LLAMA_CPP / "build" / "bin" / "llama-quantize"

    if not convert_script.exists():
        print(f"[GGUF] ERROR: llama.cpp not found at {LLAMA_CPP}")
        print(f"[GGUF] Clone it: git clone https://github.com/ggerganov/llama.cpp ~/llama.cpp && cd ~/llama.cpp && cmake -B build && cmake --build build")
        sys.exit(1)

    if not MERGED_DIR.exists():
        print(f"[GGUF] ERROR: Merged model not found at {MERGED_DIR}")
        print(f"[GGUF] Run step_merge() first")
        sys.exit(1)

    # Step 3a: HF → F16 GGUF
    print(f"[GGUF] Converting to F16 GGUF...")
    subprocess.run([
        sys.executable, str(convert_script),
        str(MERGED_DIR),
        "--outfile", str(F16_GGUF),
        "--outtype", "f16"
    ], check=True)
    print(f"[GGUF] F16 GGUF: {F16_GGUF} ({F16_GGUF.stat().st_size / 1e9:.1f} GB)")

    # Step 3b: F16 → Q4_K_M
    print(f"[GGUF] Quantizing to Q4_K_M...")
    subprocess.run([
        str(quantize_bin),
        str(F16_GGUF),
        str(Q4_GGUF),
        "Q4_K_M"
    ], check=True)
    print(f"[GGUF] Q4_K_M GGUF: {Q4_GGUF} ({Q4_GGUF.stat().st_size / 1e9:.1f} GB)")

    # Clean up F16 (6.4 GB) — only keep Q4_K_M
    if F16_GGUF.exists() and Q4_GGUF.exists():
        F16_GGUF.unlink()
        print(f"[GGUF] Cleaned up F16 intermediate file")


# ============================================================================
# MAIN
# ============================================================================

def main():
    import argparse
    parser = argparse.ArgumentParser(description="LoRA training pipeline")
    parser.add_argument("--step", choices=["train", "merge", "gguf", "all"], default="all",
                        help="Run a specific step or all (default: all)")
    parser.add_argument("--skip-train", action="store_true",
                        help="Skip training, just merge+convert existing adapter")
    parser.add_argument("--selftest", action="store_true",
                        help="Render one fake sample with the real [AVAILABLE_TOOLS] "
                             "blob and print it — no GPU, no unsloth import")
    args = parser.parse_args()

    if args.selftest:
        blob = load_tools_blob()
        conv = [
            {"from": "system", "value": "You are the TopSolid recipe sub-agent."},
            {"from": "human", "value": "Exporte la pièce active en STEP."},
            {"from": "gpt", "value": "[TOOL_CALLS]export_step[ARGS]{\"path\": \"piece.step\"}"},
        ]
        text = _render_text(conv, blob)
        ok_block = ("[AVAILABLE_TOOLS]" in text) == bool(blob)
        ok_order = text.index("[SYSTEM_PROMPT]") < text.index("[TOOL_CALLS]")
        print(f"tools_blob_chars={len(blob)}")
        print(f"has_tools_block={('[AVAILABLE_TOOLS]' in text)}")
        print(f"ok_block={ok_block} ok_order={ok_order}")
        assert ok_block and ok_order
        print("SELFTEST OK")
        return

    if args.step == "all" or args.step == "train":
        if not args.skip_train:
            model, tokenizer = step_train()
        else:
            model, tokenizer = None, None

    if args.step in ("all", "merge"):
        step_merge(
            model if args.step == "all" and not args.skip_train else None,
            tokenizer if args.step == "all" and not args.skip_train else None,
        )

    if args.step in ("all", "gguf"):
        step_convert_gguf()

    if args.step == "all":
        print(f"\n[DONE] Pipeline complete!")
        print(f"  GGUF: {Q4_GGUF}")
        # The only Modelfile in the repo is skills/topsolid-mcp/Modelfile
        # (lora-pipeline.yaml -> paths.modelfile). Its FROM line expects the GGUF
        # next to it, under the name ministral-topsolid.q4km.gguf.
        print(f"  Next: cp {Q4_GGUF} skills/topsolid-mcp/ministral-topsolid.q4km.gguf")
        print(f"        ollama create ministral-topsolid -f skills/topsolid-mcp/Modelfile")


if __name__ == "__main__":
    main()
