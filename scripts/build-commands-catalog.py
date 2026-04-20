#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build a catalog of TopSolid UI commands from the public help MD files.

POC scope (M-30 Phase 1):
    Only `help-md/EN/Cad/Drafting/**/*Command*.md` (~207 pages).
    If the tool demonstrates value in Claude Code, scale to the full
    2428 EN commands in a follow-up pass.

For each command page, extract:
    filename     e.g. "BomTableCommand"
    full_path    "Cad/Drafting/UI/Bom/BomTableCommand.md"
    title        first H1 heading
    summary      first real paragraph under the title
    menu_path    "Cad / Drafting / UI / Bom" derived from the path
    related      list of linked command filenames found in the body

Output:
    server/data/commands-catalog.json
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).parent.parent
# Adjust this glob to widen scope later (e.g. "help-md/EN/**")
# Current POC: Drafting only.
#
# The help-md tree exists only in the Cortana workspace for now (~9 MB of MD).
# Prefer the local copy if present, else look under server/data/help-md/.
HELP_ROOT_CANDIDATES = [
    Path(r"C:\Users\jup\OneDrive\Cortana\TopSolidMcpServer\data\help-md"),
    ROOT / "server" / "data" / "help-md",
    ROOT / "data" / "help-md",
]
OUT_PATH = ROOT / "server" / "data" / "commands-catalog.json"

SCOPE_GLOB = "EN/Cad/Drafting/**/*Command*.md"


def extract_title(lines: list[str]) -> str | None:
    """The TopSolid help MDs put the full title as the first non-empty plain
    line, followed by a truncated ``# First word`` header. Use the plain line."""
    for line in lines:
        s = line.strip()
        if not s:
            continue
        if s.startswith("#") or s == "---":
            continue
        return s
    return None


def extract_summary(lines: list[str]) -> str:
    """First paragraph after the first ``---`` separator."""
    past_separator = False
    buffer: list[str] = []
    for line in lines:
        s = line.rstrip()
        if not past_separator:
            if s.strip() == "---":
                past_separator = True
            continue
        if s.strip() == "---":
            break
        if s.startswith("#"):
            if buffer:
                break
            continue
        if not s.strip():
            if buffer:
                break
            continue
        buffer.append(s.strip())
        if sum(len(b) for b in buffer) > 300:
            break
    return " ".join(buffer).strip()


LINK_RE = re.compile(r"\]\(([^)]+?Command\.md)\)")


def extract_links(text: str) -> list[str]:
    return [Path(m).stem for m in LINK_RE.findall(text)]


def derive_menu_path(rel_path: Path) -> str:
    # rel_path = EN/Cad/Drafting/UI/Bom/BomTableCommand.md
    parts = list(rel_path.parts)
    # Drop language prefix and the filename itself
    if parts and parts[0] in ("EN", "FR"):
        parts = parts[1:]
    if parts:
        parts = parts[:-1]  # drop filename
    # Drop an intermediate "UI" folder for readability
    parts = [p for p in parts if p != "UI"]
    return " / ".join(parts)


def main() -> None:
    help_root = None
    for cand in HELP_ROOT_CANDIDATES:
        if cand.exists():
            help_root = cand
            break
    if help_root is None:
        print(
            "[ERR] help-md root not found. Checked: "
            + ", ".join(str(c) for c in HELP_ROOT_CANDIDATES),
            flush=True,
        )
        raise SystemExit(1)

    print(f"[INFO] help-md root: {help_root}", flush=True)
    print(f"[INFO] scope glob  : {SCOPE_GLOB}", flush=True)

    entries: list[dict] = []
    files = sorted(help_root.glob(SCOPE_GLOB))
    print(f"[INFO] {len(files)} command pages found.", flush=True)

    for f in files:
        try:
            text = f.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        lines = text.splitlines()
        title = extract_title(lines) or f.stem
        summary = extract_summary(lines)
        rel = f.relative_to(help_root)
        entry = {
            "name": f.stem,  # e.g. BomTableCommand
            "path": str(rel).replace("\\", "/"),
            "title": title,
            "summary": summary[:500] if summary else "",
            "menu": derive_menu_path(rel),
            "related": sorted(set(extract_links(text))),
        }
        entries.append(entry)

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(
        json.dumps({"version": "M-30-poc-drafting", "count": len(entries), "entries": entries},
                   ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    size_kb = OUT_PATH.stat().st_size / 1024
    print(f"[DONE] wrote {OUT_PATH} ({size_kb:.1f} KB, {len(entries)} commands)", flush=True)
    # Show a sample for sanity
    for e in entries[:3]:
        print(f"  - {e['name']:<40} | {e['title']:<35} | {e['menu']}")


if __name__ == "__main__":
    main()
