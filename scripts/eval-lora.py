#!/usr/bin/env python3
"""
Evaluation script for TopSolid LoRA fine-tuning.
Benchmarks model ability to select recipes and avoid hallucinations.

Usage:
  python eval-lora.py --model ministral:3b --output data/eval-baseline.json
  python eval-lora.py --model ministral-topsolid --output data/eval-lora.json --compare
"""

import json
import argparse
import http.client
from pathlib import Path
from datetime import datetime

# Configuration
SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR.parent / "data"
BASELINE_FILE = DATA_DIR / "eval-baseline.json"
REPORT_FILE = DATA_DIR / "eval-comparison.md"

# Load system prompt from pipeline config if available
CONFIG_FILE = SCRIPT_DIR / "lora-pipeline.yaml"
try:
    import yaml
    with open(CONFIG_FILE, "r", encoding="utf-8") as f:
        _cfg = yaml.safe_load(f)
    SYSTEM_PROMPT = _cfg["system_prompt"]
except Exception:
    SYSTEM_PROMPT = (
        "You are Noemid, a TopSolid assistant. "
        "You ONLY use topsolid__topsolid_run_recipe with a recipe name. "
        "You NEVER generate C# code. You act directly, without asking for confirmation."
    )

EVAL_SUITE = [
    # TIER 1: Trivial (Exact match with recipe name or clear keyword)
    {"tier": 1, "query": "lire la designation", "expected": "read_designation"},
    {"tier": 1, "query": "exporte en step", "expected": "export_step"},
    {"tier": 1, "query": "combien pese la piece?", "expected": "read_mass_volume"},
    {"tier": 1, "query": "save the document", "expected": "save_document"},
    {"tier": 1, "query": "liste les parametres", "expected": "read_parameters"},
    {"tier": 1, "query": "quels sont les points 3d?", "expected": "read_3d_points"},
    {"tier": 1, "query": "quels sont les reperes?", "expected": "read_3d_frames"},
    {"tier": 1, "query": "c'est un assemblage?", "expected": "detect_assembly"},
    {"tier": 1, "query": "genere un pdf", "expected": "export_pdf"},
    {"tier": 1, "query": "liste les inclusions", "expected": "list_inclusions"},

    # TIER 2: Synonyms & Informal (Slang, technical terms)
    {"tier": 2, "query": "c'est quoi le part number?", "expected": "read_reference"},
    {"tier": 2, "query": "poids total stp", "expected": "read_mass_volume"},
    {"tier": 2, "query": "donne la desig", "expected": "read_designation"},
    {"tier": 2, "query": "fait un step", "expected": "export_step"},
    {"tier": 2, "query": "y a combien de parts la dedans?", "expected": "count_assembly_parts"},
    {"tier": 2, "query": "check la piece", "expected": "audit_part"},
    {"tier": 2, "query": "rebuild tout", "expected": "rebuild_document"},
    {"tier": 2, "query": "peins en rouge", "expected": "attr_set_color"},
    {"tier": 2, "query": "c'est en quoi?", "expected": "read_material"},
    {"tier": 2, "query": "les calques svp", "expected": "attr_list_layers"},

    # TIER 3: Contextual & SI Units (Implicit context, conversions)
    {"tier": 3, "query": "longueur 50mm", "expected": "set_real_parameter"},
    {"tier": 3, "query": "hauteur 1m", "expected": "set_real_parameter"},
    {"tier": 3, "query": "quelle est la valeur de Epaisseur?", "expected": "read_real_parameter"},
    {"tier": 3, "query": "met la transparence a 50%", "expected": "attr_set_transparency"},
    {"tier": 3, "query": "echelle du plan?", "expected": "read_drafting_scale"},
    {"tier": 3, "query": "format du papier?", "expected": "read_drafting_format"},
    {"tier": 3, "query": "angle 45 deg", "expected": "set_real_parameter"},
    {"tier": 3, "query": "liste les plis", "expected": "read_bend_features"},
    {"tier": 3, "query": "dimensions du brut", "expected": "read_bounding_box"},
    {"tier": 3, "query": "cherche le doc Bride", "expected": "search_document"},

    # TIER 4: Multi-concept (Compound requests, edge cases)
    {"tier": 4, "query": "audit references et designations manquantes", "expected": "list_documents_without_reference"},
    {"tier": 4, "query": "qui utilise cette piece?", "expected": "read_where_used"},
    {"tier": 4, "query": "exporte tout le projet en step", "expected": "batch_export_step"},
    {"tier": 4, "query": "compare avec la revision d'avant", "expected": "compare_revisions"},
    {"tier": 4, "query": "vide le champ auteur partout", "expected": "batch_clear_author"},
    {"tier": 4, "query": "les pieces les plus lourdes?", "expected": "search_parts_by_material"},
    {"tier": 4, "query": "revoir l'historique des revisions", "expected": "read_revision_history"},
    {"tier": 4, "query": "les drivers sont-ils corrects?", "expected": "check_family_drivers"},
    {"tier": 4, "query": "passe en mode virtuel", "expected": "enable_virtual_document"},
    {"tier": 4, "query": "nomenclature en csv", "expected": "export_bom_csv"},

    # TIER 5: Trap & Clarification (Ambiguity, out-of-scope)
    {"tier": 5, "query": "parametre longueur", "clarification": True},
    {"tier": 5, "query": "change la couleur", "clarification": True},
    {"tier": 5, "query": "ecris m'en un script C#", "expected": None, "type": "error"},
    {"tier": 5, "query": "fais moi un cafe", "expected": None, "type": "error"},
    {"tier": 5, "query": "exporte le doc", "clarification": True},
    {"tier": 5, "query": "renomme la piece", "clarification": True},
    {"tier": 5, "query": "supprime le projet", "expected": None, "type": "error"},
    {"tier": 5, "query": "lance une simulation FEA", "expected": None, "type": "error"},
    {"tier": 5, "query": "met a jour", "clarification": True},
    {"tier": 5, "query": "prochaines vacances?", "expected": None, "type": "error"},
]


def ask_ollama(model, query):
    """Call Ollama via HTTP API."""
    try:
        conn = http.client.HTTPConnection("localhost", 11434)
        payload = json.dumps({
            "model": model,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": query}
            ],
            "stream": False,
            "options": {
                "temperature": 0.1,
                "num_predict": 150
            }
        })
        headers = {'Content-Type': 'application/json'}
        conn.request("POST", "/api/chat", payload, headers)
        res = conn.getresponse()
        data = json.loads(res.read().decode())
        return data.get("message", {}).get("content", "").strip()
    except Exception as e:
        return f"Error: {e}"


def parse_tool_call(response):
    """Extract recipe name from <tool_call>..."""
    if "<tool_call>" not in response:
        return None
    try:
        start = response.find("{")
        end = response.rfind("}") + 1
        data = json.loads(response[start:end])
        args = data.get("arguments", {})
        return args.get("recipe") or args.get("recipe_name")
    except Exception:
        return None


def evaluate_model(model, suite):
    """Run evaluation suite."""
    results = []
    print(f"Benchmarking model: {model} ({len(suite)} tests)...")

    for i, test in enumerate(suite):
        print(f"[{i+1}/{len(suite)}] Tier {test['tier']}: {test['query']}")
        response = ask_ollama(model, test["query"])

        actual_recipe = parse_tool_call(response)

        success = False
        if test.get("clarification"):
            if actual_recipe is None and ("?" in response or "Lequel" in response or "Quel" in response or "clari" in response.lower() or "which" in response.lower()):
                success = True
        elif test.get("type") == "error":
            r_lower = response.lower()
            if actual_recipe is None and (
                "ne peux pas" in r_lower
                or "pas possible" in r_lower
                or "je ne" in r_lower
                or "cannot" in r_lower
                or "can't" in r_lower
                or "not possible" in r_lower
                or "not available" in r_lower
                or "pas disponible" in r_lower
                or "i can only" in r_lower
                or "can only" in r_lower
                or "ne sont pas" in r_lower
                or "hors de" in r_lower
                or "en dehors" in r_lower
                or "trop dangereu" in r_lower
                or "trop complexe" in r_lower
            ):
                success = True
        else:
            if actual_recipe == test["expected"]:
                success = True

        results.append({
            "tier": test["tier"],
            "query": test["query"],
            "expected": test.get("expected") or "Clarification/Error",
            "actual": actual_recipe or "Natural Language",
            "response": response,
            "success": success
        })

    return results


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=str, required=True, help="Ollama model name")
    parser.add_argument("--output", type=str, help="JSON output file")
    parser.add_argument("--compare", action="store_true", help="Generate comparison report")
    parser.add_argument("--max-tests", type=int, help="Limit the number of tests")
    args = parser.parse_args()

    suite = EVAL_SUITE
    if args.max_tests:
        suite = suite[:args.max_tests]

    results = evaluate_model(args.model, suite)

    accuracy = sum(1 for r in results if r["success"]) / len(results) * 100

    summary = {
        "model": args.model,
        "timestamp": datetime.now().isoformat(),
        "total": len(results),
        "success": sum(1 for r in results if r["success"]),
        "accuracy": accuracy,
        "tiers": {}
    }

    for tier in range(1, 6):
        tier_results = [r for r in results if r["tier"] == tier]
        if tier_results:
            tier_acc = sum(1 for r in tier_results if r["success"]) / len(tier_results) * 100
        else:
            tier_acc = None
        summary["tiers"][str(tier)] = tier_acc

    print(f"\nResults for {args.model}:")
    print(f"Global Accuracy: {accuracy:.1f}%")
    for t, acc in summary["tiers"].items():
        if acc is not None:
            print(f"  Tier {t}: {acc:.1f}%")
        else:
            print(f"  Tier {t}: N/A (no tests)")

    if args.output:
        with open(args.output, "w", encoding="utf-8") as f:
            json.dump({"summary": summary, "details": results}, f, indent=2, ensure_ascii=False)
        print(f"Saved results to {args.output}")

    if args.compare and BASELINE_FILE.exists():
        with open(BASELINE_FILE, "r") as f:
            baseline = json.load(f)

        report = f"# Evaluation Report: TopSolid LoRA\n\n"
        report += f"Generated on: {datetime.now().strftime('%Y-%m-%d %H:%M')}\n\n"
        report += "## Summary\n\n"
        report += "| Metric | Baseline (ministral:3b) | LoRA (ministral-topsolid) | Delta |\n"
        report += "| :--- | :---: | :---: | :---: |\n"
        report += f"| **Global Accuracy** | {baseline['summary']['accuracy']:.1f}% | {accuracy:.1f}% | **{accuracy - baseline['summary']['accuracy']:+.1f}%** |\n"
        for t in range(1, 6):
            b_acc = baseline['summary']['tiers'][str(t)]
            l_acc = summary['tiers'][str(t)]
            report += f"| Tier {t} | {b_acc:.1f}% | {l_acc:.1f}% | {l_acc - b_acc:+.1f}% |\n"

        report += "\n## Thresholds\n\n"
        try:
            import yaml
            with open(CONFIG_FILE, "r") as f:
                cfg = yaml.safe_load(f)
            thresholds = cfg.get("eval", {})
            min_acc = thresholds.get("min_global_accuracy", 60)
            min_improvement = thresholds.get("min_improvement_over_baseline", 30)
            delta = accuracy - baseline['summary']['accuracy']
            report += f"- [{'x' if accuracy >= min_acc else ' '}] Global accuracy >= {min_acc}% (actual: {accuracy:.1f}%)\n"
            report += f"- [{'x' if delta >= min_improvement else ' '}] Improvement >= {min_improvement}pts (actual: {delta:+.1f}pts)\n"
        except Exception:
            pass

        with open(REPORT_FILE, "w", encoding="utf-8") as f:
            f.write(report)
        print(f"Generated comparison report at {REPORT_FILE}")


if __name__ == "__main__":
    main()
