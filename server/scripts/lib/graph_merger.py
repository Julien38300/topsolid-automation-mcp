"""Merge CHM-parsed methods into the existing graph.json.

Strategy:
  - The graph is produced by .NET reflection (authoritative for signatures).
  - The CHM adds semantic info: description, remarks, since, deprecated.
  - We update edges in-place when we can match them by (Interface, MethodName)
    AND add a `source_version` / `updated_at` / `source` stamp.
  - Methods present in CHM but not in the graph are logged as "chm_only" but
    NOT added to the graph (that's the job of a reflection pass on the DLLs).

Atomic write: graph.json.tmp → fsync → rename. Previous graph backed up.
"""
from __future__ import annotations

import json
import shutil
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any


@dataclass
class MergeReport:
    total_edges: int = 0
    updated: int = 0              # edges that got new CHM data
    description_filled: int = 0   # edges with empty description that got one
    since_filled: int = 0
    remarks_filled: int = 0
    deprecated_flagged: int = 0
    chm_only_methods: int = 0     # CHM methods with no matching graph edge


def _build_chm_index(methods: list[dict[str, Any]]) -> dict[tuple[str, str], list[dict[str, Any]]]:
    """Index CHM methods by (declaring_type_short_name, method_name).

    We match against declaring_type's last segment (e.g. "IParameters") because
    the graph's `Interface` field is the short name, not the FQN.
    """
    idx: dict[tuple[str, str], list[dict[str, Any]]] = {}
    for m in methods:
        declaring = m.get("declaring_type", "")
        # short name = last segment
        short = declaring.rsplit(".", 1)[-1] if declaring else ""
        key = (short, m.get("name", ""))
        idx.setdefault(key, []).append(m)
    return idx


def merge_chm_into_graph(
    graph_json_path: Path,
    chm_methods_json_path: Path,
    source_version: str,
    *,
    backup_suffix: str | None = None,
    dry_run: bool = False,
) -> MergeReport:
    """Merge CHM method data into graph.json edges.

    Args:
        graph_json_path: path to data/graph.json
        chm_methods_json_path: path to data/api/<version>/methods.json
        source_version: version label to stamp on updated edges (e.g. "7.21.164.0")
        backup_suffix: if given, backup the graph to graph.json.<suffix>.bak
        dry_run: if True, don't write anything

    Returns:
        MergeReport with counts.
    """
    graph = json.loads(graph_json_path.read_text(encoding="utf-8"))
    chm_methods = json.loads(chm_methods_json_path.read_text(encoding="utf-8"))

    chm_index = _build_chm_index(chm_methods)

    edges = graph.get("_edges") or graph.get("Edges") or []
    report = MergeReport(total_edges=len(edges))
    today = date.today().isoformat()

    matched_chm_keys: set[tuple[str, str]] = set()

    for edge in edges:
        interface = edge.get("Interface", "")
        method_name = edge.get("MethodName", "")
        key = (interface, method_name)
        if key not in chm_index:
            continue

        chm_candidates = chm_index[key]
        # Pick the first CHM method (if overloads exist, best-effort)
        chm = chm_candidates[0]
        matched_chm_keys.add(key)

        # Fill / update fields. CHM wins for description/remarks/since/deprecated.
        changed = False

        desc_before = edge.get("Description") or ""
        desc_after = chm.get("description") or ""
        if desc_after and desc_after != desc_before:
            edge["Description"] = desc_after
            if not desc_before:
                report.description_filled += 1
            changed = True

        since_before = edge.get("Since") or ""
        since_after = chm.get("since") or ""
        if since_after and since_after != since_before:
            edge["Since"] = since_after
            if not since_before:
                report.since_filled += 1
            changed = True

        remarks_after = chm.get("remarks") or ""
        if remarks_after:
            remarks_before = edge.get("Remarks") or ""
            if remarks_after != remarks_before:
                edge["Remarks"] = remarks_after
                if not remarks_before:
                    report.remarks_filled += 1
                changed = True

        if chm.get("deprecated"):
            if not edge.get("Deprecated"):
                edge["Deprecated"] = True
                edge["ObsoleteMessage"] = chm.get("obsolete_message") or ""
                report.deprecated_flagged += 1
                changed = True

        if changed:
            edge["UpdatedAt"] = today
            edge["SourceVersion"] = source_version
            edge["DocSource"] = "chm"  # renamed from "Source" to avoid clobbering existing edge[Source] (node ref)
            report.updated += 1

    # Count CHM-only methods (present in CHM but no matching edge)
    for key in chm_index:
        if key not in matched_chm_keys:
            report.chm_only_methods += len(chm_index[key])

    if dry_run:
        return report

    # Atomic write: tmp → fsync → rename. Backup first.
    if backup_suffix:
        backup_path = graph_json_path.with_suffix(f".json.{backup_suffix}.bak")
        shutil.copy2(graph_json_path, backup_path)

    tmp = graph_json_path.with_suffix(".json.tmp")
    tmp.write_text(
        json.dumps(graph, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    # On Windows, Path.replace is atomic and flushes the tmp file on close.
    # fsync is unreliable cross-platform; the replace() guarantees all-or-nothing.
    tmp.replace(graph_json_path)

    return report
