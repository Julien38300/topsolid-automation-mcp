"""Diff engine for TopSolid API snapshots.

Compares two snapshot `methods.json` files (or a snapshot vs the legacy
`graph.json` in bootstrap mode) and emits a structured diff report.

Diff key: `fully_qualified_name + "::" + normalized_signature`
  Two overloads differ only by normalized_signature (so they're distinct keys).

Categories emitted:
  - added:              new methods (absent in prev)
  - removed:            gone from new (present in prev)
  - changed_signature:  same FQN, different normalized_signature
                        (actually detected as added+removed of same FQN pair)
  - changed_description: same key, description differs
  - deprecated:          deprecated=True in new but not in prev (or new and was absent)
  - undeprecated:        deprecated=False in new but True in prev
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .api_model import ApiMethod, dict_to_method, method_key


@dataclass
class DiffSummary:
    added: int = 0
    removed: int = 0
    changed_signature: int = 0  # same FQN, sig changed
    changed_description: int = 0
    deprecated: int = 0
    undeprecated: int = 0


def load_methods_from_snapshot(methods_json: Path) -> dict[str, ApiMethod]:
    """Load methods.json and return a dict keyed by method_key."""
    raw = json.loads(methods_json.read_text(encoding="utf-8"))
    out: dict[str, ApiMethod] = {}
    for d in raw:
        m = dict_to_method(d)
        out[method_key(m)] = m
    return out


def load_methods_from_legacy_graph(graph_json: Path) -> dict[str, ApiMethod]:
    """Build a synthetic dict of ApiMethod from legacy graph.json edges.

    Used in bootstrap mode (no previous snapshot exists).
    Lossy: the graph doesn't carry deprecated/remarks/return_type cleanly.
    """
    from .api_model import normalize_signature

    raw = json.loads(graph_json.read_text(encoding="utf-8"))
    # Support both naming conventions (C# PascalCase vs runtime _snake_case)
    edges = raw.get("_edges") or raw.get("Edges") or []
    out: dict[str, ApiMethod] = {}
    for e in edges:
        interface = e.get("Interface", "")
        method_name = e.get("MethodName", "")
        if not interface or not method_name:
            continue
        # Reconstruct a basic ApiMethod record. We don't have the namespace,
        # so we'll just use a best-effort prefix.
        source = e.get("Source", {})
        source_fqn = source.get("TypeName", "")
        namespace = ".".join(source_fqn.split(".")[:-1]) if "." in source_fqn else ""
        fqn = f"{source_fqn}.{method_name}" if source_fqn else f"{interface}.{method_name}"
        signature = e.get("MethodSignature", f"{method_name}()")
        m = ApiMethod(
            fully_qualified_name=fqn,
            name=method_name,
            declaring_type=source_fqn or interface,
            namespace=namespace,
            signature=signature,
            normalized_signature=normalize_signature(signature),
            parameters=[],  # legacy graph doesn't store them
            return_type=e.get("Target", {}).get("TypeName", ""),
            description=e.get("Description", "") or "",
            remarks="",
            since=e.get("Since"),
            deprecated=("[Obsolete" in signature),
            obsolete_message=None,
            source_file="<legacy graph.json>",
        )
        out[method_key(m)] = m
    return out


def diff_methods(prev: dict[str, ApiMethod], curr: dict[str, ApiMethod]) -> dict[str, Any]:
    """Return a structured diff report.

    Results are sorted for stable output (deterministic diff files).
    """
    prev_keys = set(prev.keys())
    curr_keys = set(curr.keys())

    added_keys = sorted(curr_keys - prev_keys)
    removed_keys = sorted(prev_keys - curr_keys)
    common_keys = prev_keys & curr_keys

    # For added/removed, group by FQN to detect "changed signature"
    # (same FQN appearing in both added and removed = signature change)
    added_by_fqn: dict[str, list[str]] = {}
    for k in added_keys:
        fqn = k.split("::", 1)[0]
        added_by_fqn.setdefault(fqn, []).append(k)
    removed_by_fqn: dict[str, list[str]] = {}
    for k in removed_keys:
        fqn = k.split("::", 1)[0]
        removed_by_fqn.setdefault(fqn, []).append(k)

    # A FQN that appears in BOTH added_by_fqn and removed_by_fqn = signature change
    # (unless it's an overload being added alongside a separate overload being removed — rare)
    changed_sig_pairs: list[tuple[str, str]] = []
    pure_added_keys: list[str] = []
    pure_removed_keys: list[str] = []

    for fqn, a_keys in added_by_fqn.items():
        r_keys = removed_by_fqn.get(fqn, [])
        # Pair them 1:1 when counts match; otherwise treat all as pure add/remove
        if len(a_keys) == 1 and len(r_keys) == 1:
            changed_sig_pairs.append((r_keys[0], a_keys[0]))
        else:
            pure_added_keys.extend(a_keys)
    for fqn, r_keys in removed_by_fqn.items():
        if fqn in added_by_fqn and len(added_by_fqn[fqn]) == 1 and len(r_keys) == 1:
            continue  # already paired as changed_sig
        pure_removed_keys.extend(r_keys)

    # Changed description & (un)deprecated on common keys
    changed_desc: list[str] = []
    newly_deprecated: list[str] = []
    undeprecated: list[str] = []
    for k in sorted(common_keys):
        p = prev[k]
        c = curr[k]
        if (p.description or "") != (c.description or ""):
            changed_desc.append(k)
        if (not p.deprecated) and c.deprecated:
            newly_deprecated.append(k)
        elif p.deprecated and (not c.deprecated):
            undeprecated.append(k)

    # Build the summary
    summary = DiffSummary(
        added=len(pure_added_keys),
        removed=len(pure_removed_keys),
        changed_signature=len(changed_sig_pairs),
        changed_description=len(changed_desc),
        deprecated=len(newly_deprecated),
        undeprecated=len(undeprecated),
    )

    def method_to_dict(m: ApiMethod) -> dict[str, Any]:
        from .api_model import method_to_dict as _m
        return _m(m)

    return {
        "summary": summary.__dict__,
        "added": [method_to_dict(curr[k]) for k in pure_added_keys],
        "removed": [method_to_dict(prev[k]) for k in pure_removed_keys],
        "changed_signature": [
            {
                "before": method_to_dict(prev[before_k]),
                "after": method_to_dict(curr[after_k]),
            }
            for before_k, after_k in changed_sig_pairs
        ],
        "changed_description": [
            {
                "fqn": curr[k].fully_qualified_name,
                "before": prev[k].description,
                "after": curr[k].description,
            }
            for k in changed_desc
        ],
        "deprecated": [method_to_dict(curr[k]) for k in newly_deprecated],
        "undeprecated": [method_to_dict(curr[k]) for k in undeprecated],
    }


def build_diff(
    prev_methods_json: Path | None,
    curr_methods_json: Path,
    prev_graph_json: Path | None,
    prev_version_label: str,
    curr_version_label: str,
) -> dict[str, Any]:
    """Build a full diff object with version labels."""
    if prev_methods_json and prev_methods_json.exists():
        prev = load_methods_from_snapshot(prev_methods_json)
        mode = "incremental"
    elif prev_graph_json and prev_graph_json.exists():
        prev = load_methods_from_legacy_graph(prev_graph_json)
        mode = "bootstrap"
    else:
        prev = {}
        mode = "initial"

    curr = load_methods_from_snapshot(curr_methods_json)

    diff = diff_methods(prev, curr)
    diff["mode"] = mode
    diff["from_version"] = prev_version_label
    diff["to_version"] = curr_version_label
    diff["prev_count"] = len(prev)
    diff["curr_count"] = len(curr)
    return diff
