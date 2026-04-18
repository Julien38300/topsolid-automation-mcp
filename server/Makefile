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

# ============================================================================
# TopSolid API Sync Pipeline
# ============================================================================
# Usage:
#   make sync-api          # Full pipeline: extract CHM -> parse -> diff -> enrich -> propose -> report
#   make sync-extract      # Extract CHM from newest TopSolid install
#   make sync-parse        # Parse extracted HTML to methods.json/types.json
#   make sync-diff         # Diff against previous snapshot (or graph.json in bootstrap)
#   make sync-enrich       # Merge CHM data into graph.json (backed up)
#   make sync-propose      # Generate recipe proposals (Green/Yellow/Red tiers)
#   make sync-report       # Generate markdown changelog
#   make sync-dry-run      # Show what would happen without writing files

.PHONY: sync-api sync-extract sync-parse sync-diff sync-enrich sync-propose sync-report sync-dry-run sync-deps

sync-deps:
	$(PYTHON) -m pip install -r $(SCRIPTS)/requirements-sync.txt

sync-api:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py all

sync-extract:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py extract

sync-parse:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py parse

sync-diff:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py diff

sync-enrich:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py enrich

sync-propose:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py propose

sync-report:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py report

sync-dry-run:
	$(PYTHON) $(SCRIPTS)/sync-topsolid-api.py all --dry-run

# ============================================================================
# Phase 9b — Codestral-22B Code Sub-Agent
# ============================================================================
# Usage:
#   make code-baseline      # Eval vanilla Codestral-22B before training
#   make code-dataset       # Build the training dataset (~1500 entries)
#   make code-train         # Train LoRA on Codestral-22B (WSL2, RTX 5090)
#   make code-eval          # Eval the fine-tuned model

.PHONY: code-baseline code-dataset code-train code-eval code-deploy

code-baseline:
	$(PYTHON) $(SCRIPTS)/eval-code.py --model codestral:22b --output data/eval-code-baseline.json

code-dataset:
	$(PYTHON) $(SCRIPTS)/generate-code-dataset.py

code-train:
	$(PYTHON) $(SCRIPTS)/train-code-lora.py

code-eval:
	$(PYTHON) $(SCRIPTS)/eval-code.py --model codestral-topsolid --output data/eval-code-lora.json
