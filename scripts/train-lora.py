import os
import torch
from unsloth import FastLanguageModel
from datasets import load_dataset
from trl import SFTTrainer
from transformers import TrainingArguments
from unsloth import is_bfloat16_supported

# 1. Configuration
model_name = "unsloth/Ministral-3-3B-Instruct-2512-bnb-4bit" # Tag exact Unsloth 4-bit
max_seq_length = 2048
dtype = None # None for auto detection
load_in_4bit = True # Use 4bit quantization

# 2. Load Model & Tokenizer
model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = model_name,
    max_seq_length = max_seq_length,
    dtype = dtype,
    load_in_4bit = load_in_4bit,
)

# 3. Add LoRA Adapters
model = FastLanguageModel.get_peft_model(
    model,
    r = 32, # Rank
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj",
                      "gate_proj", "up_proj", "down_proj",],
    lora_alpha = 64,
    lora_dropout = 0,
    bias = "none",
    use_gradient_checkpointing = "unsloth",
    random_state = 42,
    use_rslora = False,
    loftq_config = None,
)

# 4. Data Preparation (Mistral [INST] format — matches Ollama template)
def formatting_prompts_func(examples):
    convs = examples["conversations"]
    texts = []
    for conv in convs:
        system_text = ""
        user_text = ""
        assistant_text = ""
        for msg in conv:
            if msg["from"] == "system":
                system_text = msg["value"]
            elif msg["from"] == "human":
                user_text = msg["value"]
            elif msg["from"] == "gpt":
                assistant_text = msg["value"]
        # Mistral Instruct format (matches ministral-3:3b template)
        text = f"[SYSTEM_PROMPT]{system_text}[/SYSTEM_PROMPT][INST]{user_text}[/INST]{assistant_text}</s>"
        texts.append(text)
    return { "text" : texts, }

dataset = load_dataset("json", data_files={"train": "data/lora-dataset.jsonl"}, split="train")
dataset = dataset.map(formatting_prompts_func, batched = True,)

# 5. Training
trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = dataset,
    dataset_text_field = "text",
    max_seq_length = max_seq_length,
    dataset_num_proc = 2,
    packing = False, # Can make training 5x faster for short sequences.
    args = TrainingArguments(
        per_device_train_batch_size = 2,
        gradient_accumulation_steps = 4,
        warmup_steps = 5,
        max_steps = -1, # Train full epochs
        num_train_epochs = 3,
        learning_rate = 2e-4,
        fp16 = not is_bfloat16_supported(),
        bf16 = is_bfloat16_supported(),
        logging_steps = 1,
        optim = "adamw_8bit",
        weight_decay = 0.01,
        lr_scheduler_type = "linear",
        seed = 42,
        output_dir = "outputs",
    ),
)

trainer_stats = trainer.train()

# 6. Save & Export
# Save LoRA adapter
model.save_pretrained("topsolid_lora")
tokenizer.save_pretrained("topsolid_lora")

# Export to GGUF (Ready for Ollama)
print("Exporting to GGUF (Q4_K_M)...")
model.save_pretrained_gguf("model-topsolid-gguf", tokenizer, quantization_method = "q4_k_m")
