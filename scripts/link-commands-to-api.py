#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build cross-links between Layer 2 (UI Commands) and Layer 1 (Automation API).

For each of the 2428 UI commands in commands-catalog.json, derive a list of
likely-related Automation API interfaces and methods from the graph. Two
signals are combined:

1. **Namespace alignment** — the FullName of a command (e.g.
   ``TopSolid.Cad.Drafting.UI.Bom.BomTableCommand``) contains segments
   that map directly to interface names:
     - segment "Bom"      -> interface IBoms
     - segment "Shapes"   -> interface IShapes
     - segment "Dimensions" -> interface IDimensions
   Generic container segments (UI, Commands, Base, Operations, Tooling,
   Configurations, tolerancing) are ignored.

2. **Method-name matching** — the command's leaf token (e.g.
   ``Extruded`` from ``ExtrudedCommand``) is searched as a substring in
   every API method name. Matches are ranked by how many keywords of the
   command body the method signature also contains (light re-ranking).

Output: ``server/data/commands-api-links.json`` — a dict keyed by command
FullName, each entry holding:
    {
      "interfaces": ["IBoms", "IDraftings", ...],
      "methods":    [{"interface": "IShapes", "name": "CreateExtrudedShape",
                       "signature": "...", "score": 12}, ...]
    }

The linker is best-effort — it produces *candidate* mappings, never
claims equivalence. The MCP tool that surfaces these results makes the
uncertainty explicit to the caller.
"""
from __future__ import annotations

import json
import re
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).parent.parent
COMMANDS = ROOT / "server" / "data" / "commands-catalog.json"
GRAPH    = ROOT / "server" / "data" / "graph.json"
OUT      = ROOT / "server" / "data" / "commands-api-links.json"

# Segments we do NOT treat as domain indicators (they appear in every
# FullName and would pollute the link). Case-sensitive to the exact
# segment spelling observed in the catalog.
GENERIC_SEGMENTS = {
    "TopSolid", "Cad", "Kernel", "Pdm", "Cae", "Cam", "Erp", "WorkManager",
    "UI", "Commands", "Base", "Operations", "Configurations",
    "Tooling", "Design", "Drafting", "Bending",
    "D2", "D3", "Ui",
    "tolerancing", "dimensions",   # lowercase container words
    "Detailing",
}


def load_interfaces(graph: dict) -> tuple[dict[str, str], dict[str, list[dict]]]:
    """Return (interfaceName -> singular stem, interfaceName -> methods)."""
    iface_stems: dict[str, str] = {}
    iface_methods: dict[str, list[dict]] = {}

    # Interfaces via graph nodes starting with I + uppercase
    for node in graph.get("_nodes", {}).values():
        full = node.get("TypeName", "")
        short = full.rsplit(".", 1)[-1]
        if short.startswith("I") and len(short) > 1 and short[1].isupper():
            # Stem: drop leading 'I', drop trailing 's' (IBoms -> Bom, IShapes -> Shape)
            stem = short[1:]
            if stem.endswith("s") and len(stem) > 2:
                stem = stem[:-1]
            iface_stems[short] = stem
            iface_methods[short] = []

    # Methods via graph edges
    for e in graph.get("_edges", []):
        iface = e.get("Interface")
        if not iface or iface not in iface_methods:
            continue
        iface_methods[iface].append({
            "name":      e.get("MethodName", ""),
            "signature": e.get("MethodSignature", ""),
        })

    return iface_stems, iface_methods


def match_interfaces_by_namespace(full_name: str, iface_stems: dict[str, str]) -> list[str]:
    """Find interfaces whose stem matches a meaningful segment of the FullName."""
    segments = [s for s in full_name.split(".") if s and s not in GENERIC_SEGMENTS]
    matches: list[str] = []
    for seg in segments:
        # Normalize (singular, drop trailing 's')
        seg_singular = seg[:-1] if seg.endswith("s") and len(seg) > 2 else seg
        seg_lower = seg_singular.lower()
        for iname, stem in iface_stems.items():
            if stem.lower() == seg_lower and iname not in matches:
                matches.append(iname)
    return matches


_COMMAND_SUFFIX_RE = re.compile(r"(Command|command)$")


def leaf_token(command_name: str) -> str:
    """'ExtrudedCommand' -> 'Extruded'. 'bominclusioncommand' -> 'bominclusion'."""
    return _COMMAND_SUFFIX_RE.sub("", command_name)


def match_methods_by_token(
    command_name: str,
    summary_tokens: set[str],
    iface_methods: dict[str, list[dict]],
    top_k: int = 4,
) -> list[dict]:
    """Rank API methods by token overlap with the command."""
    token = leaf_token(command_name).lower()
    if not token or len(token) < 3:
        return []

    candidates: list[tuple[int, str, dict]] = []
    for iname, methods in iface_methods.items():
        for m in methods:
            mname = m["name"].lower()
            msig  = m["signature"].lower()
            # Primary: substring match between token and method name
            if token in mname:
                score = 10 + len(token)  # longer token match = stronger signal
                # Bonus: method signature also contains summary keywords
                for kw in summary_tokens:
                    if len(kw) > 3 and kw in msig:
                        score += 2
                candidates.append((score, iname, m))

    candidates.sort(key=lambda x: x[0], reverse=True)
    return [
        {"interface": iname, "name": m["name"], "signature": m["signature"], "score": score}
        for score, iname, m in candidates[:top_k]
    ]


_WORD_RE = re.compile(r"[a-z]+")


def summary_tokens(entry: dict) -> set[str]:
    """Extract meaningful lowercase words from title + summary."""
    text = (entry.get("title", "") + " " + entry.get("summary", "")).lower()
    tokens = set(_WORD_RE.findall(text))
    # Drop stopwords + very short
    STOP = {"the", "a", "an", "to", "of", "in", "is", "and", "for", "on", "as", "at",
            "this", "that", "it", "be", "or", "by", "with", "from", "you", "can",
            "allow", "allows", "create", "creates", "define", "defines"}
    return {t for t in tokens if len(t) > 2 and t not in STOP}


def main() -> None:
    cmds = json.loads(COMMANDS.read_text(encoding="utf-8"))
    graph = json.loads(GRAPH.read_text(encoding="utf-8"))

    iface_stems, iface_methods = load_interfaces(graph)
    print(f"[INFO] {len(iface_stems)} interfaces, "
          f"{sum(len(ms) for ms in iface_methods.values())} methods loaded from graph")

    out: dict[str, dict] = {}
    by_iface_count = Counter()
    no_link = 0

    for entry in cmds["entries"]:
        full = entry["fullName"]
        ifaces = match_interfaces_by_namespace(full, iface_stems)
        methods = match_methods_by_token(entry["name"], summary_tokens(entry), iface_methods)

        # Add the interfaces of matched methods (even if namespace didn't pick them up)
        for m in methods:
            if m["interface"] not in ifaces:
                ifaces.append(m["interface"])

        if not ifaces and not methods:
            no_link += 1
            continue

        out[full] = {"interfaces": ifaces, "methods": methods}
        for i in ifaces:
            by_iface_count[i] += 1

    OUT.write_text(
        json.dumps({"version": "M-30-phase4", "count": len(out), "links": out},
                   ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    size_kb = OUT.stat().st_size / 1024

    print(f"[DONE] wrote {OUT} ({size_kb:.1f} KB)")
    print(f"       total commands : {len(cmds['entries'])}")
    print(f"       with links     : {len(out)}")
    print(f"       without links  : {no_link}")
    print(f"       top interfaces : {by_iface_count.most_common(8)}")

    # Spot checks
    print("\n--- Spot checks ---")
    for fn in [
        "TopSolid.Cad.Drafting.UI.Bom.BomTableCommand",
        "TopSolid.Kernel.UI.D3.Shapes.Extruded.ExtrudedCommand",
        "TopSolid.Kernel.UI.D3.Points.MidpointCommand",
    ]:
        if fn in out:
            link = out[fn]
            print(f"{fn}")
            print(f"  interfaces: {link['interfaces']}")
            for m in link["methods"][:2]:
                print(f"  method    : {m['interface']}.{m['name']} (score {m['score']})")
        else:
            print(f"{fn}  (no link)")


if __name__ == "__main__":
    main()
