# LoRA Pipeline — TopSolid Recipe Agent
# Usage:
#   make lora           # Full pipeline: validate -> generate -> train -> export -> eval
#   make lora-dataset   # Regenerate dataset from RecipeTool.cs
#   make lora-train     # Train LoRA (WSL2 auto-detected)
#   make lora-eval      # Evaluate model against benchmark
#   make lora-validate  # Validate dataset integrity
#   make lora-dry-run   # Show what would run

PYTHON ?= python
SCRIPTS = scripts

.PHONY: lora lora-dataset lora-train lora-eval lora-validate lora-dry-run lora-export lora-clean

lora:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step all

lora-dataset:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step dataset

lora-train:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step train

lora-export:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step export

lora-eval:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step eval

lora-validate:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step validate

lora-dry-run:
	$(PYTHON) $(SCRIPTS)/lora-pipeline.py --step all --dry-run

lora-clean:
	@echo "Cleaning training outputs..."
	rm -rf outputs/
	@echo "Done. (Models and dataset preserved.)"
