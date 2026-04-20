#!/usr/bin/env python3
"""Privacy scanner — fails if any published artifact contains private-corpus markers.

Runs on every push via .github/workflows/privacy-scan.yml.

Rationale:
Earlier versions of graph.json embedded ~1193 verbatim C# snippets copied
from private corpora (contributor real names, customer project folders,
TOPSOLID SAS copyright headers). This script ensures that pattern cannot
come back.

Exit code 0 = clean. Non-zero = contamination found; CI must block the push.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).parent.parent

# File globs to scan — everything that can be published.
# Do NOT scan node_modules, venv, build output, or the docs/.vitepress cache.
SCAN_GLOBS = [
    "data/**/*.json",
    "data/**/*.jsonl",
    "data/**/*.md",
    "server/data/**/*.json",
    "server/data/**/*.jsonl",
    "server/data/**/*.txt",
    "server/src/**/*.cs",
    "scripts/**/*.py",
    "docs/*.md",
    "docs/guide/*.md",
    "docs/reference/*.md",
    "docs/public/*.json",
    "README.md",
    "research/**/*.md",
]

# Literal markers that must never appear in published artifacts.
# Matching is case-sensitive to minimize false positives on common words.
#
# Note: bare "TOPSOLID SAS" is NOT listed because it appears in legitimate
# trademark attributions ("marque déposée de TOPSOLID SAS"). The actual
# leak pattern — an assembly copyright attribute copied verbatim from
# private code — is caught by the "Copyright REDACTED" / "Copyright (c)"
# patterns below.
BAD_MARKERS = [
    "REDACTED-USER",
    "REDACTED",
    "REDACTED",
    "REDACTED",          # legacy name, typically leaks via Windows paths
    "REDACTED.cs",
    "REDACTED.cs",
    "RedactedMaker",
    "RedactedViewModel",
    "Copyright REDACTED",
    "Copyright REDACTED",
    "AssemblyRedacted",  # catches any copyright-attribute leak regardless of wording
]

# Explicit allow-list: some files are allowed to mention a marker (e.g.
# this scanner itself, or local-only tool source code that references a
# user-local path). Use glob-style patterns.
ALLOW = [
    "scripts/privacy-scan.py",
    # SearchExamplesTool.cs references the user-local corpus paths by
    # design — they only exist on the author's disk, never shipped, and
    # the labels have been made opaque. Reviewed manually.
    "server/src/Tools/SearchExamplesTool.cs",
]


def is_allowed(rel_path: str) -> bool:
    return any(Path(rel_path).match(p) for p in ALLOW)


def main() -> int:
    findings: list[tuple[str, int, str, str]] = []  # (path, line, marker, excerpt)

    for pattern in SCAN_GLOBS:
        for p in ROOT.glob(pattern):
            if not p.is_file():
                continue
            rel = str(p.relative_to(ROOT)).replace("\\", "/")
            if is_allowed(rel):
                continue
            try:
                text = p.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for lineno, line in enumerate(text.splitlines(), 1):
                for marker in BAD_MARKERS:
                    if marker in line:
                        excerpt = line.strip()
                        if len(excerpt) > 120:
                            excerpt = excerpt[:117] + "..."
                        findings.append((rel, lineno, marker, excerpt))

    if not findings:
        print("[OK] privacy scan: no private-corpus markers found.")
        return 0

    print(f"[FAIL] privacy scan: {len(findings)} contaminated line(s) found.")
    # Group by path
    by_path: dict[str, list[tuple[int, str, str]]] = {}
    for path, line, marker, excerpt in findings:
        by_path.setdefault(path, []).append((line, marker, excerpt))
    for path, items in sorted(by_path.items()):
        print(f"\n  {path}  ({len(items)} hits)")
        for line, marker, excerpt in items[:5]:
            print(f"    L{line:>5}  [{marker}]  {excerpt}")
        if len(items) > 5:
            print(f"    ... and {len(items)-5} more")
    print("\nAction: strip the contaminated content before committing.")
    print("        See scripts/privacy-scan.py BAD_MARKERS list.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
