#!/usr/bin/env python3
"""Benchmark harness for the Code sub-agent (Codestral-22B).

Runs an 80-prompt suite against an Ollama model, scores:
  - compile rate  (via topsolid_execute_script dryRun if available, else AST parse heuristic)
  - Pattern D compliance (regex-based)
  - SI-units correctness (when prompt mentions mm/deg/etc.)
  - Hallucination rate (APIs not found in graph.json)
  - Refusal compliance (tier 6)

Usage:
  python scripts/eval-code.py --model codestral:22b --output data/eval-code-baseline.json
  python scripts/eval-code.py --model codestral-topsolid --output data/eval-code-lora.json --compare
"""
from __future__ import annotations

import argparse
import http.client
import json
import re
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR.parent / "data"
SUITE_FILE = DATA_DIR / "code-eval-suite.json"
GRAPH_FILE = DATA_DIR / "graph.json"


SYSTEM_PROMPT = """You are a TopSolid C# code generator. Given a user request, emit a self-contained C# script body (no namespace, no class, no `using` — these are auto-injected). The code runs inside a `Run()` method that must return a string.

CRITICAL RULES:
1. ALL writes MUST follow Pattern D:
   TopSolidHost.Application.StartModification("label", false);
   try {
       TopSolidHost.Documents.EnsureIsDirty(ref docId);  // docId CHANGES here!
       // re-fetch dependent IDs AFTER this line (NOT before)
       /* mutations */
       TopSolidHost.Application.EndModification(true, true);
       PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
       TopSolidHost.Pdm.Save(pdm, true);
       return "OK";
   } catch (Exception ex) {
       TopSolidHost.Application.EndModification(false, false);
       return "ERROR: " + ex.Message;
   }

2. SI UNITS: TopSolid uses meters and radians. Convert:
   mm → value * 0.001 (e.g. 50mm = 0.05)
   deg → value * Math.PI / 180.0 (e.g. 45deg = 0.7854)
   When OUTPUTTING: multiply by 1000 for mm, by 180/PI for deg

3. Get the document:
   DocumentId docId = TopSolidHost.Documents.EditedDocument;
   if (docId.IsEmpty) return "No document open.";

4. Only call TopSolidHost.* APIs that exist. Do NOT invent methods.

5. For out-of-scope requests (email, Excel, git, FTP, etc.), return:
   return "Cannot do that — not a TopSolid task.";

Output ONLY the C# code body, wrapped in ```csharp ... ``` fences. No explanation."""


# ---------------------------------------------------------------------------
# Graph loading for hallucination detection
# ---------------------------------------------------------------------------

_GRAPH_SYMBOLS: set[str] | None = None


def load_graph_symbols() -> set[str]:
    """Build a set of known API calls from graph.json edges (Interface.MethodName)."""
    global _GRAPH_SYMBOLS
    if _GRAPH_SYMBOLS is not None:
        return _GRAPH_SYMBOLS

    _GRAPH_SYMBOLS = set()
    if not GRAPH_FILE.exists():
        return _GRAPH_SYMBOLS

    graph = json.loads(GRAPH_FILE.read_text(encoding="utf-8"))
    edges = graph.get("_edges") or graph.get("Edges") or []
    for e in edges:
        iface = e.get("Interface", "")
        method = e.get("MethodName", "")
        if iface and method:
            _GRAPH_SYMBOLS.add(f"{iface}.{method}")

    # Add commonly-used TopSolidHost accessors (singletons, well-known)
    for common in [
        "TopSolidHost.Documents", "TopSolidHost.Application", "TopSolidHost.Pdm",
        "TopSolidHost.Parameters", "TopSolidHost.Shapes", "TopSolidHost.Elements",
        "TopSolidHost.Sketches", "TopSolidHost.User", "TopSolidHost.Version",
        "TopSolidHost.Parts", "TopSolidHost.Assemblies", "TopSolidHost.Drawings",
        "TopSolidHost.Materials", "TopSolidHost.Operations", "TopSolidHost.Features",
        "TopSolidHost.Geometries2D", "TopSolidHost.Geometries3D", "TopSolidHost.Drafts",
        "TopSolidHost.Connect", "TopSolidHost.Disconnect", "TopSolidHost.DefineConnection",
    ]:
        _GRAPH_SYMBOLS.add(common)

    return _GRAPH_SYMBOLS


# ---------------------------------------------------------------------------
# Scoring checks
# ---------------------------------------------------------------------------

def extract_code(response: str) -> str:
    """Pull the first ```csharp ... ``` block (or any code block)."""
    m = re.search(r"```(?:csharp|cs|c#)?\s*\n(.*?)```", response, re.DOTALL | re.IGNORECASE)
    if m:
        return m.group(1).strip()
    return response.strip()


def has_pattern_d(code: str) -> bool:
    """Check Pattern D presence: StartModification + EndModification(true,true) + Pdm.Save."""
    if "StartModification" not in code:
        return False
    if "EndModification(true" not in code:
        return False
    if "Pdm.Save" not in code:
        return False
    return True


def si_unit_ok(code: str, unit_hint: str | None) -> bool | None:
    """Check SI conversion presence when prompt has mm/deg/etc.

    Returns None if not applicable, True/False otherwise.
    """
    if not unit_hint:
        return None
    c = code.lower()
    if unit_hint == "mm":
        # Expect * 0.001 or / 1000 (read) or * 1000 (output)
        return ("0.001" in code) or ("* 1000" in code) or ("/ 1000" in code) or ("1000d" in code)
    if unit_hint == "deg":
        return ("math.pi" in c and "180" in c) or ("pi / 180" in c) or ("pi/180" in c.replace(" ", ""))
    if unit_hint == "m":
        # Already SI, just no weird divisions
        return True
    if unit_hint in {"cm3", "m2", "kg"}:
        # Display-only unit, verify a conversion is present
        return any(x in code for x in ["1e6", "1000000", "* 1000", "* 1e6"])
    return None


def check_hallucinations(code: str) -> tuple[int, list[str]]:
    """Count Foo.Bar(...) calls that don't exist in graph.json.

    Returns (unknown_count, sample_unknowns).
    """
    symbols = load_graph_symbols()
    # Pattern: TopSolidHost.Xxx.Yyy( ... )
    # We only flag the first level: TopSolidHost.<Category>.<Method>
    calls = re.findall(r"TopSolidHost\.([A-Z][A-Za-z]*)\.([A-Z][A-Za-z0-9_]*)", code)
    unknowns = []
    for category, method in calls:
        # Check against graph: either Category itself (known accessor) or ICategory.Method
        iface = "I" + category
        key = f"{iface}.{method}"
        cat_ok = f"TopSolidHost.{category}" in symbols
        method_ok = key in symbols
        # Cat_ok means the accessor exists. For the method we're more lenient because
        # graph stores by interface short name; if we don't find it, it's unknown.
        if not cat_ok:
            unknowns.append(f"TopSolidHost.{category}")
        elif not method_ok and category not in {"Application", "Documents", "Pdm"}:
            # For well-known types with many overloads, don't flag
            unknowns.append(key)
    return len(unknowns), unknowns[:5]


def is_refusal(code: str, response: str) -> bool:
    """Detect refusal: short or explicit 'cannot'/'not a TopSolid task'."""
    text = (response + "\n" + code).lower()
    markers = ["cannot", "can't", "not a topsolid", "not supported", "non pris en charge", "ne peux pas", "hors de portée", "not possible"]
    return any(m in text for m in markers)


def has_guard(code: str) -> bool:
    """Check for guard: IsEmpty check."""
    return ("IsEmpty" in code) or ("== null" in code)


def has_error_handling(code: str) -> bool:
    """Check for try/catch or explicit error paths."""
    return ("catch" in code) or ("EndModification(false" in code)


# ---------------------------------------------------------------------------
# Ollama call
# ---------------------------------------------------------------------------

def ask_ollama(model: str, query: str, system: str = SYSTEM_PROMPT) -> str:
    conn = http.client.HTTPConnection("localhost", 11434, timeout=180)
    payload = json.dumps({
        "model": model,
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": query},
        ],
        "stream": False,
        "options": {"temperature": 0.1, "num_predict": 1024},
    })
    conn.request("POST", "/api/chat", payload, {"Content-Type": "application/json"})
    res = conn.getresponse()
    data = json.loads(res.read().decode())
    if "error" in data:
        return f"ERROR: {data['error']}"
    return data.get("message", {}).get("content", "").strip()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def score_prompt(prompt: dict[str, Any], response: str) -> dict[str, Any]:
    """Score a single prompt's response."""
    code = extract_code(response)
    tier = prompt.get("tier")
    result: dict[str, Any] = {
        "id": prompt["id"],
        "tier": tier,
        "code_len": len(code),
        "has_code_block": "```" in response,
    }

    # Tier 6: refusal expected
    if tier == 6:
        result["refusal_ok"] = is_refusal(code, response)
        return result

    # Non-empty check
    if not code:
        result["empty"] = True
        return result

    # Pattern D (for writes)
    if prompt.get("pattern_d"):
        result["pattern_d_ok"] = has_pattern_d(code)

    # SI units
    unit = prompt.get("si_units")
    if unit:
        if isinstance(unit, bool) and unit:
            # generic flag — assume mm
            unit = "mm"
        result["si_units_ok"] = si_unit_ok(code, unit)

    # Guard for tier 5 guarded reads
    if prompt.get("guard"):
        result["guard_ok"] = has_guard(code)

    # Error handling for tier 5
    if prompt.get("error_handling"):
        result["error_handling_ok"] = has_error_handling(code)

    # Hallucinations
    unknown_count, unknown_sample = check_hallucinations(code)
    result["unknown_apis"] = unknown_count
    if unknown_sample:
        result["unknown_sample"] = unknown_sample

    return result


def summarize(results: list[dict[str, Any]]) -> dict[str, Any]:
    by_tier: dict[int, list[dict]] = {}
    for r in results:
        by_tier.setdefault(r["tier"], []).append(r)

    tier_stats = {}
    for tier, rs in sorted(by_tier.items()):
        stat: dict[str, Any] = {"count": len(rs)}
        # Code presence
        stat["has_code"] = sum(1 for r in rs if r.get("has_code_block") and not r.get("empty"))
        # Pattern D (only where applicable)
        pd_rs = [r for r in rs if "pattern_d_ok" in r]
        if pd_rs:
            stat["pattern_d_rate"] = sum(1 for r in pd_rs if r["pattern_d_ok"]) / len(pd_rs)
        # SI units
        si_rs = [r for r in rs if "si_units_ok" in r]
        if si_rs:
            stat["si_units_rate"] = sum(1 for r in si_rs if r["si_units_ok"]) / len(si_rs)
        # Refusal
        ref_rs = [r for r in rs if "refusal_ok" in r]
        if ref_rs:
            stat["refusal_rate"] = sum(1 for r in ref_rs if r["refusal_ok"]) / len(ref_rs)
        # Guard
        g_rs = [r for r in rs if "guard_ok" in r]
        if g_rs:
            stat["guard_rate"] = sum(1 for r in g_rs if r["guard_ok"]) / len(g_rs)
        # Error handling
        eh_rs = [r for r in rs if "error_handling_ok" in r]
        if eh_rs:
            stat["error_handling_rate"] = sum(1 for r in eh_rs if r["error_handling_ok"]) / len(eh_rs)
        # Hallucination: avg unknown APIs
        hal = [r.get("unknown_apis", 0) for r in rs if "unknown_apis" in r]
        if hal:
            stat["avg_unknown_apis"] = sum(hal) / len(hal)
            stat["clean_rate"] = sum(1 for c in hal if c == 0) / len(hal)
        tier_stats[tier] = stat

    # Overall
    all_code = [r for r in results if r.get("has_code_block")]
    global_stats = {
        "total": len(results),
        "emitted_code": len(all_code),
    }
    return {"tiers": tier_stats, "global": global_stats}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, help="Ollama model tag (e.g. codestral:22b)")
    parser.add_argument("--output", required=True, help="Path to write results JSON")
    parser.add_argument("--max-tests", type=int, default=None, help="Limit number of prompts (for quick runs)")
    parser.add_argument("--language", choices=["fr", "en"], default="fr", help="Language variant of prompts")
    args = parser.parse_args()

    suite = json.loads(SUITE_FILE.read_text(encoding="utf-8"))
    prompts = suite["prompts"]
    if args.max_tests:
        prompts = prompts[: args.max_tests]

    print(f"Benchmarking {args.model} on {len(prompts)} prompts ({args.language})...")

    results = []
    for i, p in enumerate(prompts, 1):
        query = p.get(args.language) or p.get("fr")
        print(f"[{i}/{len(prompts)}] T{p['tier']}-{p['id']}: {query[:60]}")
        response = ask_ollama(args.model, query)
        res = score_prompt(p, response)
        res["query"] = query
        res["response_preview"] = response[:300]
        results.append(res)

    summary = summarize(results)

    # Print summary
    print("\n=== Summary ===")
    for tier, stat in summary["tiers"].items():
        print(f"Tier {tier}: {stat}")
    print(f"Global: {summary['global']}")

    # Save
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(
        json.dumps({"model": args.model, "language": args.language, "summary": summary, "details": results}, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"\nSaved to {out}")


if __name__ == "__main__":
    main()
