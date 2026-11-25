#!/usr/bin/env python3
"""
Recipe-name selection benchmark for the TopSolid LoRA model.

Usage:
  python eval-lora.py --model ministral:3b --output data/eval-baseline.json
  python eval-lora.py --model ministral-topsolid --output data/eval-lora.json --compare

SCOPE -- read this before quoting an accuracy number.

What it measures: for each prompt in EVAL_SUITE it sends one turn to Ollama,
pulls a recipe name out of the raw text with parse_tool_call(), and compares that
string to a hard-coded expected string. That is all. It is a string-match
benchmark, useful for comparing two models on the same prompts.

What it does NOT measure WITHOUT --verify-mcp:
  * Whether the recipe exists. Names are never checked against RecipeTool.cs or
    server/data/recipe-list.txt. A model that invents a plausible name is only
    ever marked wrong for differing from the expected string -- never flagged as
    non-existent -- so hallucination is not actually detected here.
  * The `value` argument. set_real_parameter with no value, or with millimetres
    where the API wants SI metres, scores as a success.
  * Anything downstream. This script never talks to the MCP server, never starts
    TopSolid, and never observes a document. Nothing here shows that a recipe ran,
    succeeded, or produced the right result.
  * The production prompt shape. No tools are advertised to Ollama, so the
    [AVAILABLE_TOOLS] block that the Modelfile TEMPLATE injects in real use --
    several thousand tokens of JSON schema for the 13 registered MCP tools -- is
    absent here (same skew as the TODO in scripts/train-lora.py).
  * Refusals, beyond substring matching. The tier-5 checks look for a handful of
    FR/EN phrases; a correct refusal worded differently counts as a failure.

So: a regression signal on recipe-name selection, not an end-to-end validation.
End-to-end checks go through the MCP server itself (see tests/), and since
T-1066 step 11 this script can do it too: `--verify-mcp` replays every parsed
model call as a real `tools/call topsolid_run_recipe` against a live
TopSolidMcpServer.exe (stdio, profile small) — an invented recipe name then
fails on "Unknown recipe", a value left in raw millimetres fails the SI check,
and results carry `mcp_verdict` / `value_check` fields. `--selftest-mcp` runs
this verification loop offline (no Ollama) against the real server binary.
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
    # Fallback only -- lora-pipeline.yaml (system_prompt) is the source of truth.
    SYSTEM_PROMPT = (
        "You are a TopSolid MCP Assistant. "
        "You ONLY use topsolid__topsolid_run_recipe with a recipe name. "
        "You NEVER generate C# code. "
        "Read-only recipes (read_, list_, count_, detect_, search_, compare_, audit_, "
        "check_, summarize_) change nothing: call them directly. "
        "Write recipes (set_, rename_, fix_, copy_, enable_, clear_, save_, rebuild_, "
        "export_, batch_, attr_set_, attr_replace_, attr_assign_, invoke_command) modify "
        "the model, the PDM data or files on disk: state the recipe and the value, then "
        "wait for the user to confirm before calling."
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
    {"tier": 2, "query": "poids total stp", "expected": "read_mass_volume", "also_accept": ["assembly_mass_report"]},
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


def parse_tool_call_args(response):
    """Extract (recipe_name, args_dict) from a tool call in either format.

    Mistral native:  [TOOL_CALLS]topsolid__topsolid_run_recipe[ARGS]{"recipe":"x"}
    Hermes:         <tool_call>{"name":"...","arguments":{"recipe":"x"}}
    Bare (Ollama /api/chat strips the native markers):
                     topsolid__topsolid_run_recipe{"recipe":"x"}
    Returns (None, None) when no call is found.
    """
    import re
    # Mistral native format
    if "[TOOL_CALLS]" in response and "[ARGS]" in response:
        try:
            args_start = response.find("[ARGS]") + len("[ARGS]")
            json_start = response.find("{", args_start)
            # Find matching end-brace (handles nested? for our case flat is fine)
            depth = 0
            json_end = json_start
            for i in range(json_start, len(response)):
                if response[i] == "{":
                    depth += 1
                elif response[i] == "}":
                    depth -= 1
                    if depth == 0:
                        json_end = i + 1
                        break
            args = json.loads(response[json_start:json_end])
            name = args.get("recipe") or args.get("recipe_name")
            return name, args if isinstance(args, dict) else {}
        except Exception:
            return None, None

    # Bare emitted call (Ollama /api/chat strips the Mistral native markers):
    #   topsolid__topsolid_run_recipe{"recipe":"x"}
    if re.search(r"\w*run_recipe\w*\s*(?=\{)", response):
        try:
            m = re.search(r"\w*run_recipe\w*\s*\{", response)
            json_start = m.end() - 1
            depth = 0
            json_end = json_start
            for i in range(json_start, len(response)):
                if response[i] == "{":
                    depth += 1
                elif response[i] == "}":
                    depth -= 1
                    if depth == 0:
                        json_end = i + 1
                        break
            args = json.loads(response[json_start:json_end])
            name = args.get("recipe") or args.get("recipe_name")
            return name, args if isinstance(args, dict) else {}
        except Exception:
            return None, None

    # Hermes format (legacy fallback)
    if "<tool_call>" in response:
        try:
            start = response.find("{")
            end = response.rfind("}") + 1
            data = json.loads(response[start:end])
            args = data.get("arguments", {})
            return args.get("recipe") or args.get("recipe_name"), args if isinstance(args, dict) else {}
        except Exception:
            return None, None

    return None, None


def parse_tool_call(response):
    """Extract recipe name only (kept for backward compatibility)."""
    return parse_tool_call_args(response)[0]


def evaluate_model(model, suite, verifier=None):
    """Run evaluation suite."""
    results = []
    print(f"Benchmarking model: {model} ({len(suite)} tests)...")
    if verifier is not None:
        print("MCP verification ENABLED: every parsed call is replayed against "
              f"{verifier.exe.name} (hallucinated names fail on 'Unknown recipe'.)")

    for i, test in enumerate(suite):
        print(f"[{i+1}/{len(suite)}] Tier {test['tier']}: {test['query']}")
        response = ask_ollama(model, test["query"])

        actual_recipe, tool_args = parse_tool_call_args(response)

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
                or "assistant topsolid" in r_lower
                or "24/7" in r_lower
            ):
                success = True
        else:
            valid_recipes = [test["expected"]] + test.get("also_accept", [])
            if actual_recipe in valid_recipes:
                success = True

        mcp_verdict = None
        value_check = None
        if verifier is not None and actual_recipe and not test.get("clarification") and test.get("type") != "error":
            mcp = verifier.verify(actual_recipe, tool_args, test["query"])
            mcp_verdict = mcp.get("verdict")
            mcp_detail = mcp.get("detail", "")
            value_check = value_argument_verdict(test["query"], tool_args)
            if mcp_verdict == "hallucinated":
                success = False  # criterion (a)/(c): the name does not exist server-side
            if value_check is not None:
                success = False  # criterion (b): wrong/missing unit conversion
        else:
            mcp_detail = ""

        results.append({
            "tier": test["tier"],
            "query": test["query"],
            "expected": test.get("expected") or "Clarification/Error",
            "actual": actual_recipe or "Natural Language",
            "response": response,
            "success": success,
            "mcp_verdict": mcp_verdict,
            "value_check": value_check,
            "mcp_detail": mcp_detail
        })

    return results


# ---------------------------------------------------------------------------
# T-1066 step 11 -- close the evaluation loop against the REAL MCP server.
# The parsed model output is replayed as a genuine tools/call against a live
# TopSolidMcpServer.exe (stdio), so a hallucinated recipe name fails on
# "Unknown recipe" instead of only differing from the expected string.
# ---------------------------------------------------------------------------
import os
import re
import subprocess
import sys
import threading
import time
import queue as _queue

MCP_BIN_DEFAULT = Path(os.environ.get(
    "TOPSOLID_MCP_BIN",
    str(SCRIPT_DIR.parent / "server" / "src" / "bin" / "Release" / "net48" / "TopSolidMcpServer.exe")))
MCP_CALL_TIMEOUT_S = float(os.environ.get("MCP_CALL_TIMEOUT_S", "12"))

# SI-multipliers for the `value` argument check (criterion b of step 11).
# The TopSolid API wants SI metres -- a model producing raw millimetres is wrong.
_SI_MULT = {"mm": 0.001, "millimetre": 0.001, "millimetres": 0.001,
            "cm": 0.01, "centimetre": 0.01,
            "dm": 0.1,
            "m": 1.0, "metre": 1.0, "metres": 1.0}


def si_expected_value(query):
    """Expected SI value (in metres) if the query carries a length with unit, else None."""
    m = re.search(r"(\d+(?:[.,]\d+)?)\s*(mm|millimetres?|cm|centimetres?|dm|m|metres?)\b",
                  query.lower())
    if not m:
        return None
    return float(m.group(1).replace(",", ".")) * _SI_MULT[m.group(2)]


def value_argument_verdict(query, args):
    """Check `value` for set_real_parameter-style calls. Returns None when OK/not applicable."""
    if args is None or not isinstance(args, dict) or "value" not in args:
        return None  # no value argument produced -- nothing to check here
    expected = si_expected_value(query)
    if expected is None:
        return None
    raw = args.get("value")
    try:
        got = float(raw)
    except (TypeError, ValueError):
        return f"non_numeric_value({raw!r})"
    if abs(got - expected) > 1e-9:
        return f"wrong_units(got={raw}, expected_si={expected})"
    return None


class McpRecipeVerifier:
    """Replays parsed model output against the real TopSolidMcpServer (stdio).

    Verdicts:
      executed_ok         -- result returned with no error and no "Unknown recipe".
      exists_not_executed -- server could not execute for a reason OTHER than an
                             unknown recipe (e.g. TopSolid not attached): the name
                             therefore exists in Recipes.
      hallucinated        -- "Unknown recipe" from the server: name does not exist.
      timeout / verifier_down -- no answer in time / server not spawnable
                             (conservative: treated as exists_not_executed).
    """

    def __init__(self, exe=None):
        self.exe = Path(exe or MCP_BIN_DEFAULT)
        self.proc = None
        self.responses = _queue.Queue()
        self._next_id = 10

    def _send(self, obj):
        self.proc.stdin.write(json.dumps(obj) + "\n")
        self.proc.stdin.flush()

    def _rpc(self, obj, timeout=None):
        req_id = obj.get("id")
        self._send(obj)
        deadline = time.time() + (timeout or MCP_CALL_TIMEOUT_S)
        while time.time() < deadline:
            try:
                msg = self.responses.get(timeout=max(0.05, deadline - time.time()))
            except _queue.Empty:
                break
            if isinstance(msg, dict) and msg.get("id") == req_id:
                return msg
        return None

    def _start(self):
        if not self.exe.exists():
            raise FileNotFoundError(str(self.exe))
        env = dict(os.environ, TOPSOLID_MCP_TRANSPORT="stdio",
                   TOPSOLID_MCP_PROFILE=os.environ.get("TOPSOLID_MCP_PROFILE", "small"))
        self.proc = subprocess.Popen(
            [str(self.exe)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL, env=env, text=True, encoding="utf-8")

        def _pump():
            try:
                for line in self.proc.stdout:
                    try:
                        self.responses.put(json.loads(line))
                    except Exception:
                        continue
            except Exception:
                pass

        t = threading.Thread(target=_pump, daemon=True)
        t.start()
        init = {"jsonrpc": "2.0", "id": 1, "method": "initialize",
                "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "t1066-eval-verify", "version": "0"}}}
        self._rpc(init, 2 * MCP_CALL_TIMEOUT_S)
        self._send({"jsonrpc": "2.0", "method": "notifications/initialized"})

    @staticmethod
    def _result_text(msg):
        result = msg.get("result", {}) if isinstance(msg, dict) else {}
        parts = []
        for c in result.get("content", []) or []:
            if isinstance(c, dict) and "text" in c:
                parts.append(c["text"])
        if "error" in msg:
            parts.append(json.dumps(msg["error"], ensure_ascii=False))
        return "\n".join(parts)

    def verify(self, recipe_name, args, query):
        """Return {"verdict": ..., "detail": ...} for one parsed model call."""
        if not recipe_name:
            return {"verdict": "no_call", "detail": ""}
        try:
            if self.proc is None or self.proc.poll() is not None:
                self._start()
        except Exception as e:
            return {"verdict": "verifier_down", "detail": str(e)[:200]}
        call = {"jsonrpc": "2.0", "id": self._next_id, "method": "tools/call",
                "params": {"name": "topsolid_run_recipe",
                           "arguments": {"recipe": recipe_name}}}
        self._next_id += 1
        msg = self._rpc(call)
        if msg is None:
            return {"verdict": "timeout", "detail": ""}
        text = self._result_text(msg)
        if "Unknown recipe" in text:
            return {"verdict": "hallucinated", "detail": text[:200]}
        if msg.get("error") or (msg.get("result", {}) or {}).get("isError"):
            return {"verdict": "exists_not_executed", "detail": text[:200]}
        return {"verdict": "executed_ok", "detail": text[:200]}

    def close(self):
        if self.proc is not None and self.proc.poll() is None:
            try:
                self.proc.stdin.close()
                self.proc.kill()
            except Exception:
                pass


def selftest_mcp():
    """Offline selftest of the MCP verification loop (no Ollama, no GPU).

    Uses fake model outputs replayed against the REAL server binary:
      1. invented recipe name  -> server must answer "Unknown recipe" -> hallucinated
      2. valid write recipe, value left in raw millimetres -> wrong_units
      3. valid write recipe, value already SI metres    -> value check OK
    """
    cases = [
        ("invented_name", '[TOOL_CALLS]topsolid__topsolid_run_recipe[ARGS]{"recipe":"read_designation_v2"}</s>',
         "read_designation_v2", "lire la designation", "hallucinated"),
        ("valid_name_raw_mm", '[TOOL_CALLS]topsolid__topsolid_run_recipe[ARGS]{"recipe":"set_real_parameter","value":50}</s>',
         "set_real_parameter", "longueur 50mm", "exists_or_executed"),
        ("valid_name_si", '[TOOL_CALLS]topsolid__topsolid_run_recipe[ARGS]{"recipe":"set_real_parameter","value":0.05}</s>',
         "set_real_parameter", "longueur 50mm", "exists_or_executed"),
    ]
    verifier = McpRecipeVerifier()
    all_ok = True
    try:
        for label, response, _recipe, query, expected_verdict in cases:
            name, args = parse_tool_call_args(response)
            assert name == _recipe, f"{label}: parse_tool_call_args -> {name!r}, {_args_err(args)}"
            mcp = verifier.verify(name, args, query)
            vverdict = value_argument_verdict(query, args)
            if expected_verdict == "hallucinated":
                ok = mcp["verdict"] == "hallucinated"
            else:
                ok = mcp["verdict"] in ("executed_ok", "exists_not_executed")
            if label == "valid_name_raw_mm":
                ok = ok and (vverdict is not None and vverdict.startswith("wrong_units"))
            if label == "valid_name_si":
                ok = ok and vverdict is None
            print(f"  {label}: name={name} mcp={mcp['verdict']} value_check={vverdict}"
                  f" -> {'OK' if ok else 'FAIL'}"
                  + (f" | detail: {mcp['detail'][:120]}" if mcp.get('detail') else ""))
            all_ok = all_ok and ok
    finally:
        verifier.close()
    print("SELFTEST MCP OK" if all_ok else "SELFTEST MCP FAILED")
    return all_ok


def _args_err(args):
    return "not a dict" if not isinstance(args, dict) else repr(args)[:80]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", type=str, required=False, help="Ollama model name")
    parser.add_argument("--output", type=str, help="JSON output file")
    parser.add_argument("--compare", action="store_true", help="Generate comparison report")
    parser.add_argument("--max-tests", type=int, help="Limit the number of tests")
    parser.add_argument("--selftest-mcp", action="store_true",
                        help="Selftest the real-server verification loop (T-1066 step 11) and exit")
    parser.add_argument("--verify-mcp", action="store_true",
                        help="Replay every parsed tool call against the real TopSolidMcpServer (closes the loop)")
    args = parser.parse_args()

    if args.selftest_mcp:
        sys.exit(0 if selftest_mcp() else 1)

    if not args.model:
        parser.error("--model is required unless --selftest-mcp is used")

    suite = EVAL_SUITE
    if args.max_tests:
        suite = suite[:args.max_tests]

    verifier = McpRecipeVerifier() if args.verify_mcp else None
    try:
        results = evaluate_model(args.model, suite, verifier=verifier)
    finally:
        if verifier is not None:
            verifier.close()

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
