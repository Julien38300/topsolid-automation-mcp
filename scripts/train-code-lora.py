#!/usr/bin/env python3
"""LoRA training for Codestral-22B TopSolid code agent.

Forked from train-lora.py with 22B-specific knobs:
- Larger rank (r=64, alpha=128)
- Longer sequences (4096)
- Batch 1 + grad_accum 8 (tight on 24GB)
- Gradient checkpointing + paged_adamw_8bit for OOM resilience
- Fallback: r=32, seq=2048 if OOM

The code agent ouputs are PURE C# (no tool_call format), so the [INST] template is plain.
"""
import os
import sys
import shutil
import subprocess
from pathlib import Path

# ============================================================================
# CONFIGURATION
# ============================================================================

MODEL_NAME = "unsloth/codestral-22b-v0.1-bnb-4bit"
MAX_SEQ_LENGTH = 4096
LORA_R = 64
LORA_ALPHA = 128
EPOCHS = 2
BATCH_SIZE = 1
GRAD_ACCUM = 8
LR = 1e-4

DATASET_FILE = os.environ.get(
    "CODE_DATASET",
    str(Path(__file__).parent.parent / "data" / "code-dataset.jsonl")
)

HOME = Path.home()
ADAPTER_DIR = HOME / "codestral_topsolid_lora"
MERGED_DIR = HOME / "codestral_topsolid_merged"
F16_GGUF = HOME / "codestral-topsolid-f16.gguf"
Q4_GGUF = HOME / "codestral-topsolid.gguf"
LLAMA_CPP = HOME / "llama.cpp"


def step_train():
    import torch
    from unsloth import FastLanguageModel, is_bfloat16_supported
    from datasets import load_dataset
    from trl import SFTTrainer
    from transformers import TrainingArguments

    print(f"[TRAIN] Loading {MODEL_NAME}")
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

    # Mistral [INST] chat template — no [TOOL_CALLS], pure code output
    def formatting_prompts_func(examples):
        convs = examples["conversations"]
        texts = []
        for conv in convs:
            system_text = user_text = assistant_text = ""
            for msg in conv:
                if msg["from"] == "system":
                    system_text = msg["value"]
                elif msg["from"] == "human":
                    user_text = msg["value"]
                elif msg["from"] == "gpt":
                    assistant_text = msg["value"]
            text = f"[SYSTEM_PROMPT]{system_text}[/SYSTEM_PROMPT][INST]{user_text}[/INST]{assistant_text}</s>"
            texts.append(text)
        return {"text": texts}

    print(f"[TRAIN] Loading dataset {DATASET_FILE}")
    dataset = load_dataset("json", data_files={"train": DATASET_FILE}, split="train")
    dataset = dataset.map(formatting_prompts_func, batched=True)
    print(f"[TRAIN] {len(dataset)} samples, {EPOCHS} epochs")

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
            warmup_steps=10,
            max_steps=-1,
            num_train_epochs=EPOCHS,
            learning_rate=LR,
            fp16=not is_bfloat16_supported(),
            bf16=is_bfloat16_supported(),
            logging_steps=5,
            optim="paged_adamw_8bit",  # more memory-efficient than adamw_8bit for 22B
            weight_decay=0.01,
            lr_scheduler_type="cosine",
            seed=42,
            output_dir="outputs-code",
        ),
    )

    stats = trainer.train()
    print(f"[TRAIN] Done! Loss: {stats.training_loss:.4f}")

    model.save_pretrained(str(ADAPTER_DIR))
    tokenizer.save_pretrained(str(ADAPTER_DIR))
    print(f"[TRAIN] Adapter saved to {ADAPTER_DIR}")


if __name__ == "__main__":
    step_train()
