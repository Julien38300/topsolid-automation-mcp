#!/usr/bin/env python3
"""Regenerate docs/public/graph-data.json for the Cytoscape viz.

Source: data/graph.json + data/api-index.json
Output: docs/public/graph-data.json

Produces a compact interface-level graph:
- Nodes: one per interface (46), with stats (methods, edges, examples, hints, descs)
  and module assignment (Kernel / Design / Drafting) derived from namespace.
- Edges: between interface pairs that share ElementId/DocumentId/PdmObjectId
  (or other non-primitive types) in their method signatures. Weight = number
  of shared types.

The viz uses this at https://<site>/reference/graphe-interactif.html
"""
from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).parent.parent
GRAPH = ROOT / "data" / "graph.json"
API_INDEX = ROOT / "data" / "api-index.json"
OUT = ROOT / "docs" / "public" / "graph-data.json"

# Type names we consider "relevant" for inter-interface connections
# (mostly the TopSolid ID types — drop strings, primitives, voids)
RELEVANT_TYPE_RE = re.compile(
    r"\b(?:PdmObjectId|PdmMajorRevisionId|PdmMinorRevisionId|"
    r"DocumentId|ElementId|ShapeId|SketchId|FamilyId|OccurrenceId|"
    r"ParameterId|PatternId|ConstraintId|BendFeatureId|ViewId|"
    r"ProjectionSetId|FormatId|BomRowId|InclusionId|AssemblyId)\b"
)


def short_type(full: str) -> str:
    """Strip namespace: 'TopSolid.Kernel.Automating.DocumentId' -> 'DocumentId'."""
    return full.rsplit(".", 1)[-1]


def module_of_namespace(ns: str) -> str:
    if ".Kernel." in ns or ns.endswith(".Kernel"):
        return "Kernel"
    if ".Drafting" in ns:
        return "Drafting"
    if ".Design" in ns or ".Cad." in ns:
        return "Design"
    return "Kernel"


def main() -> None:
    graph = json.loads(GRAPH.read_text(encoding="utf-8"))
    api = json.loads(API_INDEX.read_text(encoding="utf-8"))

    ifaces = api["interfaces"]  # { "IPdm": {"namespace": "...", "methods": [...]}, ... }

    # Collect per-interface stats from graph.json edges
    stats = defaultdict(lambda: {"edges": 0, "examples": 0, "hints": 0, "descs": 0, "types": set()})
    for e in graph["_edges"]:
        iface = e.get("Interface")
        if not iface or iface not in ifaces:
            continue
        s = stats[iface]
        s["edges"] += 1
        if e.get("Description"):
            s["descs"] += 1
        if e.get("SemanticHint"):
            s["hints"] += 1
        if e.get("Examples"):
            s["examples"] += 1
        # Collect non-primitive types this interface touches
        for side in ("Source", "Target"):
            t = e.get(side, {}).get("TypeName", "")
            short = short_type(t)
            if RELEVANT_TYPE_RE.fullmatch(short):
                s["types"].add(short)

    # Also extract types from method signatures in api-index (catches methods
    # without graph edges, e.g. param-heavy void methods)
    for iface_name, info in ifaces.items():
        for m in info.get("methods", []):
            for tok in RELEVANT_TYPE_RE.findall(m.get("signature", "")):
                stats[iface_name]["types"].add(tok)

    # Build nodes
    nodes = []
    for iface_name, info in ifaces.items():
        s = stats[iface_name]
        methods_count = len(info.get("methods", []))
        module = module_of_namespace(info.get("namespace", ""))
        nodes.append({
            "data": {
                "id": iface_name,
                "label": iface_name[1:] if iface_name.startswith("I") else iface_name,
                "methods": methods_count,
                "edges": s["edges"],
                "examples": s["examples"],
                "hints": s["hints"],
                "descs": s["descs"],
                "module": module,
                "size": max(25, methods_count),
            }
        })

    # Build edges between interfaces that share relevant types
    edges = []
    iface_list = list(ifaces.keys())
    for i, a in enumerate(iface_list):
        for b in iface_list[i + 1:]:
            shared = stats[a]["types"] & stats[b]["types"]
            if not shared:
                continue
            # Bidirectional edge; Cytoscape can render it undirected via style
            edges.append({
                "data": {
                    "source": a,
                    "target": b,
                    "weight": len(shared),
                    "types": sorted(shared),
                }
            })

    # Unique methods in the graph (by interface::name::signature)
    unique_methods = set()
    for e in graph["_edges"]:
        if e.get("Interface"):
            unique_methods.add(
                e["Interface"] + "::" + e.get("MethodName", "") + "::" + e.get("MethodSignature", "")
            )
    total_methods = len(unique_methods)
    total_edges = len(graph["_edges"])
    total_examples = sum(s["examples"] for s in stats.values())

    out = {
        "nodes": nodes,
        "edges": edges,
        "stats": {
            "totalInterfaces": len(nodes),
            "totalMethods": total_methods,
            "totalEdges": total_edges,
            "totalExamples": total_examples,
        },
    }

    OUT.write_text(json.dumps(out, ensure_ascii=False), encoding="utf-8")

    print(f"[OK] wrote {OUT}")
    print(f"     nodes={len(nodes)}  cy-edges={len(edges)}  "
          f"api-methods={total_methods}  api-edges={total_edges}  examples={total_examples}")
    # Per-module breakdown
    by_mod = defaultdict(int)
    for n in nodes:
        by_mod[n["data"]["module"]] += 1
    print(f"     modules: {dict(by_mod)}")


if __name__ == "__main__":
    main()
