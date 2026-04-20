#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Privacy scanner - fails if any published artifact contains private-corpus markers.

Runs on every push via .github/workflows/privacy-scan.yml.

Rationale:
Earlier versions of graph.json embedded ~1193 verbatim C# snippets copied
from private corpora (contributor real names, customer project folders,
TOPSOLID SAS copyright headers). History was rewritten on 2026-04-20 via
git filter-repo; this scanner guards against regression at HEAD.

Exit code 0 = clean. Non-zero = contamination found; CI must block the push.
"""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).parent.parent

# File globs to scan - everything that can be published.
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
# Case-sensitive to minimize false positives on common words.
#
# Bare "TOPSOLID SAS" is NOT listed - it appears in legitimate trademark
# attributions. The actual leak pattern (assembly copyright attribute copied
# verbatim) is caught via "Copyright REDACTED" / "AssemblyRedacted" below.
BAD_MARKERS = [
    # Build markers as strings rather than literals so filter-repo's
    # text-replacement passes do not rewrite this file itself when run on
    # the repo. Reconstructing via chr()/join avoids literal substring matches.
    chr(97) + "nne-francoise",
    chr(65) + "nne-francoise",
    chr(65) + "nne-Francoise",
    chr(65) + "nne-Fran" + chr(231) + "oise",  # with cedilla
    "REDACTED",
    "REDACTED",
    "REDACTED",
    "REDACTED.cs",
    "REDACTED.cs",
    "RedactedMaker",
    "RedactedViewModel",
    "Copyright REDACTED",
    "Copyright " + chr(169) + " TopSolid",
    "AssemblyRedacted",
    "REDACTED-USER",   # residue from filter-repo pass - should be gone at HEAD
]

ALLOW = [
    "scripts/privacy-scan.py",
    "server/src/Tools/SearchExamplesTool.cs",
]


def is_allowed(rel_path: str) -> bool:
    return any(Path(rel_path).match(p) for p in ALLOW)


def main() -> int:
    findings: list[tuple[str, int, str, str]] = []

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

    print("[FAIL] privacy scan: " + str(len(findings)) + " contaminated line(s) found.")
    by_path: dict[str, list[tuple[int, str, str]]] = {}
    for path, line, marker, excerpt in findings:
        by_path.setdefault(path, []).append((line, marker, excerpt))
    for path, items in sorted(by_path.items()):
        print("\n  " + path + "  (" + str(len(items)) + " hits)")
        for line, marker, excerpt in items[:5]:
            # ASCII-safe to avoid cp1252 issues in Windows CI
            safe = excerpt.encode("ascii", errors="replace").decode("ascii")
            print("    L" + str(line).rjust(5) + "  [" + marker + "]  " + safe)
        if len(items) > 5:
            print("    ... and " + str(len(items) - 5) + " more")
    print("\nAction: strip the contaminated content before committing.")
    print("        See scripts/privacy-scan.py BAD_MARKERS list.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
