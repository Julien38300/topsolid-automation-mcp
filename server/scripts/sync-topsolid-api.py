#!/usr/bin/env python3
"""TopSolid API sync pipeline — extract CHM -> parse -> diff -> enrich graph.

Usage:
    # Full pipeline (auto-detect CHM from newest TopSolid install)
    python scripts/sync-topsolid-api.py all

    # Extract only
    python scripts/sync-topsolid-api.py extract --chm-path "C:/Program Files/TOPSOLID/TopSolid 7.21/Help/en/TopSolid'Design Automation.chm"

    # Auto-detect and extract
    python scripts/sync-topsolid-api.py extract

    # Run from a specific stage
    python scripts/sync-topsolid-api.py all --from-stage parse

Stages:
    extract    CHM -> data/api/<version>/raw/*.htm + meta.json
    parse      raw/*.htm -> methods.json, types.json, namespaces.json
    diff       compare with previous snapshot -> api-diff-<version>.json
    enrich     merge into graph.json (atomic write + backup)
    propose    suggest recipes (Green/Yellow/Red tiers) -> recipe-proposals-<version>.md
    tests      generate TestSuite stubs for proposals
    report     changelog-<version>.md
    all        run all stages
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

# Make `from lib.xxx import yyy` work when invoked via `python scripts/sync-topsolid-api.py`
_SCRIPT_DIR = Path(__file__).parent.resolve()
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib import (  # noqa: E402
    api_model, chm_extractor, differ, graph_merger, html_parser,
    paths, recipe_proposer, reporter,
)


STAGES = ["extract", "parse", "diff", "enrich", "propose", "tests", "report"]


# ============================================================================
# STAGE: extract
# ============================================================================

def stage_extract(args: argparse.Namespace) -> str:
    """Extract CHM -> data/api/<version>/raw/. Returns the version string."""
    if args.chm_path:
        chm = Path(args.chm_path)
    else:
        chm = paths.auto_detect_chm()
        if chm is None:
            print("[extract] ERROR: no TopSolid install found under C:/Program Files/TOPSOLID/", file=sys.stderr)
            print("          Pass --chm-path explicitly.", file=sys.stderr)
            sys.exit(1)

    print(f"[extract] CHM: {chm}")

    # We don't know the exact version until we peek at the CHM,
    # so extract to a staging dir first, then move.
    install_version = chm_extractor.parse_install_version(chm)
    # Use install_version as a tentative snapshot dir; we'll rename if assembly_version differs.
    tentative_dir = paths.API_DIR / f"tmp-extract-{install_version}"

    # Check if a snapshot already exists and matches this CHM hash — idempotency
    existing_version_match = None
    if args.skip_if_hash_match:
        chm_hash = chm_extractor.sha256_file(chm)
        if paths.API_DIR.exists():
            for d in paths.API_DIR.iterdir():
                if not d.is_dir() or d.name == "current":
                    continue
                meta = d / "meta.json"
                if meta.exists():
                    import json as _json
                    try:
                        m = _json.loads(meta.read_text(encoding="utf-8"))
                        if m.get("chm_sha256") == chm_hash:
                            existing_version_match = d.name
                            break
                    except Exception:
                        pass
        if existing_version_match:
            print(f"[extract] CHM hash matches existing snapshot {existing_version_match} — skipping.")
            return existing_version_match

    # Extract to tentative dir
    raw_dir = tentative_dir / "raw"
    result = chm_extractor.extract(chm, raw_dir, prefer_7z=True)
    print(f"[extract] {result.html_file_count} HTML files -> {raw_dir}")
    print(f"[extract] Assembly version: {result.assembly_version}")
    print(f"[extract] TopSolid install version: {result.topsolid_version}")
    print(f"[extract] Extractor: {result.extractor}")

    # The canonical version is the assembly version (e.g. 7.21.164.0)
    final_version = result.assembly_version if result.assembly_version != "unknown" else result.topsolid_version
    if not paths.VERSION_RE.match(final_version):
        print(f"[extract] ERROR: could not determine a valid version (got {final_version!r})", file=sys.stderr)
        sys.exit(1)

    # Move tmp-extract-X -> final version dir
    final_dir = paths.snapshot_dir(final_version)
    if final_dir.exists():
        import shutil as _shutil
        _shutil.rmtree(final_dir)
    final_dir.parent.mkdir(parents=True, exist_ok=True)
    tentative_dir.rename(final_dir)

    # Write meta.json
    chm_extractor.write_meta(paths.snapshot_meta_json(final_version), result)

    # Update `current` symlink
    try:
        paths.set_current_link(final_version)
        print(f"[extract] `current` symlink -> {final_version}")
    except Exception as e:
        print(f"[extract] WARNING: couldn't update `current` symlink: {e}", file=sys.stderr)

    print(f"[extract] DONE -> {final_dir}")
    return final_version


# ============================================================================
# STAGE: parse (TODO — M-SYNC-2)
# ============================================================================

def stage_parse(args: argparse.Namespace, version: str) -> None:
    """Parse raw HTML -> methods.json, types.json, namespaces.json."""
    raw_dir = paths.snapshot_raw_dir(version)
    if not raw_dir.exists():
        print(f"[parse] ERROR: raw dir missing: {raw_dir}", file=sys.stderr)
        sys.exit(1)

    print(f"[parse] Parsing {raw_dir}...")
    methods, types = html_parser.parse_directory(raw_dir)
    print(f"[parse] Parsed {len(methods)} methods, {len(types)} types")

    # Drop results with empty signatures (they're not methods we can use)
    methods_valid = [m for m in methods if m.signature]
    if len(methods_valid) != len(methods):
        print(f"[parse] WARNING: {len(methods) - len(methods_valid)} methods have empty signatures (skipped)")

    # Build namespaces index
    from collections import defaultdict
    ns_index: dict[str, list[str]] = defaultdict(list)
    for t in types:
        ns_index[t.namespace].append(t.fully_qualified_name)
    for ns in ns_index:
        ns_index[ns].sort()
    ns_list = sorted(ns_index.keys())

    if args.dry_run:
        print(f"[parse] [DRY RUN] would write methods.json ({len(methods_valid)} records)")
        print(f"[parse] [DRY RUN] would write types.json ({len(types)} records)")
        print(f"[parse] [DRY RUN] would write namespaces.json ({len(ns_list)} namespaces)")
        return

    api_model.write_json(
        paths.snapshot_methods_json(version),
        [api_model.method_to_dict(m) for m in methods_valid],
    )
    api_model.write_json(
        paths.snapshot_types_json(version),
        [api_model.type_to_dict(t) for t in types],
    )
    api_model.write_json(
        paths.snapshot_namespaces_json(version),
        {ns: ns_index[ns] for ns in ns_list},
    )
    print(f"[parse] DONE -> {paths.snapshot_dir(version)}")
    print(f"[parse]   methods.json:    {len(methods_valid)} records")
    print(f"[parse]   types.json:      {len(types)} records")
    print(f"[parse]   namespaces.json: {len(ns_list)} namespaces")


# ============================================================================
# STAGE: diff (TODO — M-SYNC-3)
# ============================================================================

def stage_diff(args: argparse.Namespace, version: str) -> None:
    """Diff current snapshot vs previous snapshot (or legacy graph.json in bootstrap mode)."""
    curr_methods_path = paths.snapshot_methods_json(version)
    if not curr_methods_path.exists():
        print(f"[diff] ERROR: {curr_methods_path} not found. Run `parse` first.", file=sys.stderr)
        sys.exit(1)

    prev_snapshot_dir = paths.find_previous_snapshot(version)
    if prev_snapshot_dir:
        prev_methods_path = prev_snapshot_dir / "methods.json"
        prev_label = prev_snapshot_dir.name
        print(f"[diff] Comparing vs previous snapshot: {prev_label}")
    else:
        prev_methods_path = None
        prev_label = "bootstrap"
        print(f"[diff] No previous snapshot — bootstrap mode (comparing vs current graph.json)")

    diff = differ.build_diff(
        prev_methods_json=prev_methods_path,
        curr_methods_json=curr_methods_path,
        prev_graph_json=paths.GRAPH_JSON if paths.GRAPH_JSON.exists() else None,
        prev_version_label=prev_label,
        curr_version_label=version,
    )

    # Print summary
    s = diff["summary"]
    print(f"[diff] Mode: {diff['mode']}")
    print(f"[diff] {diff['prev_count']} methods in prev, {diff['curr_count']} in new")
    print(f"[diff] Summary:")
    print(f"[diff]   + Added:               {s['added']}")
    print(f"[diff]   - Removed:             {s['removed']}")
    print(f"[diff]   ~ Changed signature:   {s['changed_signature']}")
    print(f"[diff]   ~ Changed description: {s['changed_description']}")
    print(f"[diff]   ! Newly deprecated:    {s['deprecated']}")
    print(f"[diff]   ! Undeprecated:        {s['undeprecated']}")

    if args.dry_run:
        print(f"[diff] [DRY RUN] would write {paths.diff_json(version)}")
        return

    api_model.write_json(paths.diff_json(version), diff)
    print(f"[diff] DONE -> {paths.diff_json(version)}")


# ============================================================================
# STAGE: enrich (TODO — M-SYNC-4)
# ============================================================================

def stage_enrich(args: argparse.Namespace, version: str) -> None:
    """Merge CHM method data into graph.json (atomic + backup)."""
    if not paths.GRAPH_JSON.exists():
        print(f"[enrich] ERROR: {paths.GRAPH_JSON} not found", file=sys.stderr)
        sys.exit(1)

    curr_methods_path = paths.snapshot_methods_json(version)
    if not curr_methods_path.exists():
        print(f"[enrich] ERROR: {curr_methods_path} not found. Run `parse` first.", file=sys.stderr)
        sys.exit(1)

    print(f"[enrich] Merging CHM data into {paths.GRAPH_JSON}...")
    print(f"[enrich]   source: {curr_methods_path}")
    print(f"[enrich]   version stamp: {version}")

    report = graph_merger.merge_chm_into_graph(
        graph_json_path=paths.GRAPH_JSON,
        chm_methods_json_path=curr_methods_path,
        source_version=version,
        backup_suffix=f"pre-{version}",
        dry_run=args.dry_run,
    )

    print(f"[enrich] Total edges:            {report.total_edges}")
    print(f"[enrich] Edges updated:          {report.updated}")
    print(f"[enrich] Descriptions filled:    {report.description_filled}")
    print(f"[enrich] Since versions filled:  {report.since_filled}")
    print(f"[enrich] Remarks filled:         {report.remarks_filled}")
    print(f"[enrich] Deprecated flagged:     {report.deprecated_flagged}")
    print(f"[enrich] CHM-only (no edge):     {report.chm_only_methods}")

    if args.dry_run:
        print(f"[enrich] [DRY RUN] No changes written.")
    else:
        print(f"[enrich] DONE -> {paths.GRAPH_JSON}")


# ============================================================================
# STAGE: propose (TODO — M-SYNC-5)
# ============================================================================

def stage_propose(args: argparse.Namespace, version: str) -> None:
    """Propose recipes for CHM-only methods (not yet in graph) — Green/Yellow/Red tiers."""
    methods_path = paths.snapshot_methods_json(version)
    if not methods_path.exists():
        print(f"[propose] ERROR: {methods_path} not found. Run `parse` first.", file=sys.stderr)
        sys.exit(1)

    import json as _json
    chm_methods = _json.loads(methods_path.read_text(encoding="utf-8"))

    # Load graph to identify which CHM methods don't have a matching edge
    if not paths.GRAPH_JSON.exists():
        print(f"[propose] ERROR: graph.json not found", file=sys.stderr)
        sys.exit(1)
    graph = _json.loads(paths.GRAPH_JSON.read_text(encoding="utf-8"))
    edges = graph.get("_edges") or graph.get("Edges") or []

    # Build a set of (Interface short, MethodName) from the graph
    graph_keys = {
        (e.get("Interface", ""), e.get("MethodName", ""))
        for e in edges
        if e.get("Interface") and e.get("MethodName")
    }

    chm_only = []
    for m in chm_methods:
        interface_short = m.get("declaring_type", "").rsplit(".", 1)[-1]
        key = (interface_short, m.get("name", ""))
        if key not in graph_keys:
            chm_only.append(m)

    print(f"[propose] Found {len(chm_only)} CHM-only methods (no matching graph edge)")

    proposals = recipe_proposer.propose_recipes(chm_only)
    green = sum(1 for p in proposals if p.tier == "green")
    yellow = sum(1 for p in proposals if p.tier == "yellow")
    red = sum(1 for p in proposals if p.tier == "red")

    print(f"[propose] Tiers: 🟢 Green={green}  🟡 Yellow={yellow}  🔴 Red={red}")

    if args.dry_run:
        print(f"[propose] [DRY RUN] would write proposals to {paths.proposals_md(version)}")
        return

    recipe_proposer.write_proposals_md(proposals, paths.proposals_md(version), version)
    recipe_proposer.write_proposals_json(proposals, paths.snapshot_proposals_json(version))
    print(f"[propose] DONE -> {paths.proposals_md(version)}")
    print(f"[propose]        {paths.snapshot_proposals_json(version)}")


def stage_tests(args: argparse.Namespace, version: str) -> None:
    """Placeholder — test stubs would go here. Deferred."""
    print(f"[tests] (deferred) — test stub generation not yet implemented")


def stage_report(args: argparse.Namespace, version: str) -> None:
    """Generate the changelog markdown for this sync run."""
    diff_path = paths.diff_json(version)
    if not diff_path.exists():
        print(f"[report] ERROR: {diff_path} not found. Run `diff` first.", file=sys.stderr)
        sys.exit(1)

    proposals_path = paths.snapshot_proposals_json(version)
    if not proposals_path.exists():
        proposals_path = None

    out = paths.changelog_md(version)
    if args.dry_run:
        print(f"[report] [DRY RUN] would write changelog to {out}")
        return

    reporter.generate_changelog(
        version=version,
        diff_path=diff_path,
        proposals_json_path=proposals_path,
        out_path=out,
    )
    print(f"[report] DONE -> {out}")


# ============================================================================
# CLI
# ============================================================================

def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description="TopSolid API sync pipeline",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    p.add_argument(
        "stage",
        choices=STAGES + ["all"],
        help="Stage to run (or 'all' for the full pipeline)",
    )
    p.add_argument(
        "--chm-path",
        type=str,
        default=None,
        help="Path to the CHM file. If omitted, auto-detect newest TopSolid install.",
    )
    p.add_argument(
        "--version",
        type=str,
        default=None,
        help="Explicit snapshot version to operate on (skips re-extraction). "
        "Required for stages other than `extract` when no `current` symlink exists.",
    )
    p.add_argument(
        "--from-stage",
        type=str,
        default=None,
        choices=STAGES,
        help="When running 'all', start from this stage (skip previous ones).",
    )
    p.add_argument(
        "--skip-if-hash-match",
        action="store_true",
        default=True,
        help="In extract stage, skip if a snapshot with the same CHM hash already exists (default: on).",
    )
    p.add_argument(
        "--no-skip-if-hash-match",
        dest="skip_if_hash_match",
        action="store_false",
        help="Force re-extraction even if hash matches.",
    )
    p.add_argument(
        "--dry-run",
        action="store_true",
        help="Print what would happen without writing files.",
    )
    return p


def resolve_version(args: argparse.Namespace) -> str:
    """Figure out which snapshot to operate on for post-extract stages."""
    if args.version:
        return args.version
    if paths.API_CURRENT_LINK.exists():
        return paths.API_CURRENT_LINK.resolve().name
    print("[error] No --version given and no `current` symlink found. Run `extract` first.", file=sys.stderr)
    sys.exit(1)


def main() -> None:
    args = build_parser().parse_args()

    if args.stage == "all":
        stages_to_run = STAGES
        if args.from_stage:
            i = STAGES.index(args.from_stage)
            stages_to_run = STAGES[i:]

        version = None
        for st in stages_to_run:
            if st == "extract":
                version = stage_extract(args)
            else:
                if version is None:
                    version = resolve_version(args)
                globals()[f"stage_{st}"](args, version)
        return

    # Single stage
    if args.stage == "extract":
        stage_extract(args)
    else:
        version = resolve_version(args)
        globals()[f"stage_{args.stage}"](args, version)


if __name__ == "__main__":
    main()
