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
# Quality gate
# ============================================================================
# CLAUDE.md requires `make check` to pass before RecipeTool.cs is committed.
# Usage:
#   make check            # privacy scan + recipe catalogue check
#   make check-privacy    # scripts/privacy-scan.py only
#   make check-recipes    # scripts/check-recipes.py only
#   make sync-ecosystem   # NOT implemented - prints what still has to be done

.PHONY: check check-privacy check-recipes sync-ecosystem

check: check-privacy check-recipes

check-privacy:
	$(PYTHON) $(SCRIPTS)/privacy-scan.py

check-recipes:
	$(PYTHON) $(SCRIPTS)/check-recipes.py

# NOT IMPLEMENTED. CLAUDE.md tells contributors to run this after every recipe
# change, but no script has ever stood behind the name. It prints the steps that
# still have to be done by hand and exits 0, rather than claiming to have
# synchronised anything.
sync-ecosystem:
	@echo "sync-ecosystem is NOT implemented - nothing has been synchronised."
	@echo "After changing server/src/Tools/RecipeTool.cs, update by hand:"
	@echo "  data/recipes.md, server/data/recipes.md   - recipe reference"
	@echo "  server/data/recipe-list.txt               - manifest shipped in the release"
	@echo "  skills/                                   - recipe list exposed to agents"
	@echo "  docs/                                     - documentation site"
	@echo "  make lora-dataset                         - LoRA training dataset"
	@echo "Then run: make check"
