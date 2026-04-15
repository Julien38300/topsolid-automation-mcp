#!/usr/bin/env python3
"""
LoRA Pipeline Orchestrator — end-to-end automation.

Chains: validate -> generate dataset -> train -> export GGUF -> import Ollama -> eval -> compare

Usage:
  python lora-pipeline.py                    # Full pipeline
  python lora-pipeline.py --step dataset     # Just regenerate dataset
  python lora-pipeline.py --step train       # Just train (assumes dataset exists)
  python lora-pipeline.py --step eval        # Just evaluate
  python lora-pipeline.py --step validate    # Just validate dataset
  python lora-pipeline.py --dry-run          # Show what would run
"""

import argparse
import json
import os
import subprocess
import sys
import time
import yaml
from datetime import datetime
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_DIR = SCRIPT_DIR.parent
CONFIG_FILE = SCRIPT_DIR / "lora-pipeline.yaml"


def load_config():
    """Load pipeline configuration."""
    with open(CONFIG_FILE, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def log(msg, level="INFO"):
    """Print timestamped log message."""
    ts = datetime.now().strftime("%H:%M:%S")
    prefix = {"INFO": "   ", "OK": " + ", "WARN": " ! ", "ERR": " X ", "STEP": "=> "}
    print(f"[{ts}] {prefix.get(level, '   ')} {msg}")


def run_cmd(cmd, cwd=None, check=True):
    """Run a command and stream output."""
    log(f"Running: {' '.join(cmd)}")
    result = subprocess.run(
        cmd,
        cwd=cwd or str(PROJECT_DIR),
        capture_output=False,
        text=True,
    )
    if check and result.returncode != 0:
        log(f"Command failed with exit code {result.returncode}", "ERR")
        sys.exit(1)
    return result


def step_validate(config):
    """Validate dataset before training."""
    log("Validating dataset...", "STEP")

    dataset_path = PROJECT_DIR / config["paths"]["dataset"]
    mapping_path = PROJECT_DIR / config["paths"]["recipe_mapping"]

    if not dataset_path.exists():
        log(f"Dataset not found: {dataset_path}", "ERR")
        return False

    if not mapping_path.exists():
        log(f"Recipe mapping not found: {mapping_path}", "ERR")
        return False

    # Load and validate entries
    with open(dataset_path, "r", encoding="utf-8") as f:
        lines = [l.strip() for l in f if l.strip()]

    with open(mapping_path, "r", encoding="utf-8") as f:
        mapping = json.load(f)

    en_names = set(mapping.values())
    fr_names = set(mapping.keys())

    errors = 0
    warnings = 0
    tool_calls = 0
    fr_recipes_found = []

    for i, line in enumerate(lines, 1):
        try:
            entry = json.loads(line)
        except json.JSONDecodeError:
            log(f"  Line {i}: Invalid JSON", "ERR")
            errors += 1
            continue

        convs = entry.get("conversations", [])
        if not convs:
            log(f"  Line {i}: Empty conversations", "WARN")
            warnings += 1
            continue

        # Check structure: system + human + gpt
        roles = [c["from"] for c in convs]
        if roles != ["system", "human", "gpt"]:
            log(f"  Line {i}: Unexpected role sequence: {roles}", "WARN")
            warnings += 1

        # Check for FR recipe names in tool calls
        gpt_msg = next((c["value"] for c in convs if c["from"] == "gpt"), "")
        if "<tool_call>" in gpt_msg:
            tool_calls += 1
            for fr_name in fr_names:
                if f'"recipe":"{fr_name}"' in gpt_msg or f'"recipe": "{fr_name}"' in gpt_msg:
                    fr_recipes_found.append((i, fr_name))

    log(f"  {len(lines)} entries, {tool_calls} tool_calls, {errors} errors, {warnings} warnings")

    if fr_recipes_found:
        log(f"  {len(fr_recipes_found)} entries still use FR recipe names!", "ERR")
        for line_num, name in fr_recipes_found[:5]:
            log(f"    Line {line_num}: {name}", "ERR")
        errors += len(fr_recipes_found)

    if errors == 0:
        log("Dataset validation passed", "OK")
        return True
    else:
        log(f"Dataset validation FAILED ({errors} errors)", "ERR")
        return False


def step_dataset(config):
    """Regenerate the LoRA dataset from RecipeTool.cs."""
    log("Generating LoRA dataset...", "STEP")
    run_cmd(
        [sys.executable, str(SCRIPT_DIR / "generate-lora-dataset.py")],
        cwd=str(PROJECT_DIR),
    )

    # Show stats
    stats_path = PROJECT_DIR / config["paths"]["dataset_stats"]
    if stats_path.exists():
        with open(stats_path, "r", encoding="utf-8") as f:
            stats = json.load(f)
        log(f"  Generated {stats['total_entries']} entries, {stats['recipes_count']} recipes", "OK")
    else:
        log("  Stats file not generated", "WARN")


def step_train(config, dry_run=False):
    """Run LoRA training in WSL2."""
    log("Starting LoRA training...", "STEP")

    # Check if running in WSL or Windows
    is_wsl = os.path.exists("/proc/version")

    if is_wsl:
        log("  Detected WSL2 environment")
        run_cmd(
            [sys.executable, str(SCRIPT_DIR / "train-lora.py")],
            cwd=str(PROJECT_DIR),
        )
    else:
        # Windows — need to call into WSL2
        log("  Detected Windows — launching training via WSL2")
        wsl_project = str(PROJECT_DIR).replace("C:\\", "/mnt/c/").replace("\\", "/")
        wsl_cmd = (
            f"cd {wsl_project} && "
            f"source ~/lora-env/bin/activate && "
            f"python3 scripts/train-lora.py"
        )
        if dry_run:
            log(f"  [DRY RUN] wsl bash -c '{wsl_cmd}'")
            return
        run_cmd(["wsl", "bash", "-c", wsl_cmd])

    log("Training complete", "OK")


def step_export(config, dry_run=False):
    """Export GGUF and import into Ollama."""
    log("Exporting to Ollama...", "STEP")

    modelfile_path = PROJECT_DIR / config["paths"]["modelfile"]
    model_tag = config["model"]["ollama_tag"]

    if not modelfile_path.exists():
        log(f"Modelfile not found: {modelfile_path}", "ERR")
        sys.exit(1)

    if dry_run:
        log(f"  [DRY RUN] ollama create {model_tag} -f {modelfile_path}")
        return

    run_cmd(["ollama", "create", model_tag, "-f", str(modelfile_path)])
    log(f"  Model imported as '{model_tag}'", "OK")


def step_eval(config, dry_run=False):
    """Run evaluation benchmark."""
    log("Running evaluation...", "STEP")

    eval_script = SCRIPT_DIR / "eval-lora.py"
    model_tag = config["model"]["ollama_tag"]
    output_path = PROJECT_DIR / config["paths"]["eval_lora"]

    if dry_run:
        log(f"  [DRY RUN] python eval-lora.py --model {model_tag}")
        return

    # Check Ollama is running
    try:
        import http.client
        conn = http.client.HTTPConnection("localhost", 11434, timeout=3)
        conn.request("GET", "/api/tags")
        conn.getresponse()
        conn.close()
    except Exception:
        log("Ollama is not running! Start it first.", "ERR")
        sys.exit(1)

    # Run baseline if no baseline exists
    baseline_path = PROJECT_DIR / config["paths"]["eval_baseline"]
    if not baseline_path.exists():
        log("  No baseline found — running baseline first...")
        run_cmd([
            sys.executable, str(eval_script),
            "--model", "ministral:3b",
            "--output", str(baseline_path),
        ])

    # Run LoRA eval
    run_cmd([
        sys.executable, str(eval_script),
        "--model", model_tag,
        "--output", str(output_path),
        "--compare",
    ])

    # Check thresholds
    if output_path.exists():
        with open(output_path, "r", encoding="utf-8") as f:
            results = json.load(f)
        accuracy = results["summary"]["accuracy"]
        min_acc = config["eval"]["min_global_accuracy"]
        if accuracy >= min_acc:
            log(f"  Global accuracy: {accuracy:.1f}% (threshold: {min_acc}%)", "OK")
        else:
            log(f"  Global accuracy: {accuracy:.1f}% < {min_acc}% threshold", "WARN")

    log("Evaluation complete", "OK")


def main():
    parser = argparse.ArgumentParser(description="LoRA Pipeline Orchestrator")
    parser.add_argument(
        "--step",
        choices=["validate", "dataset", "train", "export", "eval", "all"],
        default="all",
        help="Which step to run (default: all)",
    )
    parser.add_argument("--dry-run", action="store_true", help="Show what would run without executing")
    parser.add_argument("--skip-validate", action="store_true", help="Skip dataset validation")
    parser.add_argument("--config", type=str, help="Path to config YAML (default: lora-pipeline.yaml)")
    args = parser.parse_args()

    if args.config:
        global CONFIG_FILE
        CONFIG_FILE = Path(args.config)

    config = load_config()

    print()
    print("=" * 60)
    print("  LoRA Pipeline — TopSolid Recipe Agent")
    print(f"  Model: {config['model']['base']}")
    print(f"  Step:  {args.step}")
    if args.dry_run:
        print("  Mode:  DRY RUN")
    print("=" * 60)
    print()

    t0 = time.time()

    steps = {
        "validate": lambda: step_validate(config),
        "dataset": lambda: step_dataset(config),
        "train": lambda: step_train(config, args.dry_run),
        "export": lambda: step_export(config, args.dry_run),
        "eval": lambda: step_eval(config, args.dry_run),
    }

    if args.step == "all":
        order = ["validate", "dataset", "train", "export", "eval"]
        for step_name in order:
            if step_name == "validate" and args.skip_validate:
                continue
            result = steps[step_name]()
            if result is False:  # validate returns False on failure
                log("Pipeline aborted due to validation failure", "ERR")
                sys.exit(1)
    else:
        result = steps[args.step]()
        if result is False:
            sys.exit(1)

    elapsed = time.time() - t0
    print()
    log(f"Pipeline finished in {elapsed:.0f}s", "OK")


if __name__ == "__main__":
    main()
