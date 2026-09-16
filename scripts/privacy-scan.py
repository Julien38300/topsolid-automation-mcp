#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Privacy scanner - fails if any published file leaks private data.

Runs on every push via .github/workflows/privacy-scan.yml.

Two independent detectors:

1. BAD_MARKERS - a literal blacklist of strings known to have leaked in the
   past (contributor real names, customer project folders, verbatim assembly
   copyright headers). It is a regression guard: by construction it can only
   catch a leak that already happened once.
2. Pattern detectors - regular expressions matching the *shape* of a leak:
   Windows user directories, UNC shares, e-mail addresses and the usual API
   credentials. This is what catches the next, unknown leak.

Rationale for (1):
Earlier versions of graph.json embedded ~1193 verbatim C# snippets copied
from private corpora (contributor real names, customer project folders,
TOPSOLID SAS copyright headers). History was rewritten on 2026-04-20 via
git filter-repo; this scanner guards against regression at HEAD.

Exit code 0 = clean. Non-zero = contamination found; CI must block the push.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).parent.parent

# Everything that is published. Build output, dependencies and binary payloads
# are pruned by EXCLUDED_DIRS / TEXT_SUFFIXES below, so these globs can stay
# broad on purpose: a glob that is too narrow is exactly how the leaks this
# scanner is meant to catch stayed invisible.
SCAN_GLOBS = [
    "*.md",
    "*.json",
    "*.yml",
    "*.yaml",
    "Makefile",
    "LICENSE",
    ".gitignore",
    ".github/**/*",
    "bridge/**/*",
    "data/**/*",
    "docs/**/*",
    "graph/**/*",
    "research/**/*",
    "scripts/**/*",
    "server/**/*",
    "skills/**/*",
    "tests/**/*",
]

# Directory names that are never published: build output, dependencies,
# caches, VCS/IDE metadata.
EXCLUDED_DIRS = {
    ".git",
    ".vs",
    ".idea",
    "__pycache__",
    "bin",
    "obj",
    "packages",
    "node_modules",
    "dist",
    "cache",
}

# Only text files are read. Anything else (help.db, images, assemblies) is
# skipped - a binary blob would produce noise, not findings.
TEXT_SUFFIXES = {
    ".bat", ".cfg", ".cmd", ".config", ".cs", ".csproj", ".css", ".editorconfig",
    ".html", ".ini", ".js", ".json", ".jsonl", ".jsx", ".cjs", ".mjs", ".md",
    ".markdown", ".props", ".ps1", ".psm1", ".py", ".resx", ".sh", ".sln",
    ".sql", ".targets", ".toml", ".ts", ".mts", ".tsx", ".txt", ".xml", ".yaml",
    ".yml",
}
TEXT_FILENAMES = {"Makefile", "LICENSE", ".gitignore"}

# Literal markers that must never appear in published artifacts.
# Case-sensitive to minimize false positives on common words.
#
# Bare "TOPSOLID SAS" is NOT listed - it appears in legitimate trademark
# attributions. The actual leak pattern (assembly copyright attribute copied
# verbatim) is caught via "Copyright REDACTED" / "AssemblyRedacted" below.
BAD_MARKERS = [
    # EVERY marker is obfuscated via chr() / string concatenation so a
    # filter-repo text-replacement pass never rewrites this file itself.
    # A literal "REDACTED" or "REDACTED.cs" would be rewritten by our replacements
    # file on the next privacy pass, silently breaking the scanner.
    chr(97) + "nne-francoise",
    chr(65) + "nne-francoise",
    chr(65) + "nne-Francoise",
    chr(65) + "nne-Fran" + chr(231) + "oise",  # with cedilla
    "st" + "uyk",
    "EG" + "GER",
    "MIS" + "SLER",
    "For" + "m1.cs",
    "Au" + "tre.cs",
    "RoBSt" + "ockParametersMaker",
    "MainWin" + "dowViewModel",
    "Copyright (c) To" + "pSolid",
    "Copyright " + chr(169) + " To" + "pSolid",
    "Assembly" + "Copyright",
    "REDACTED-" + "USER",   # residue from a filter-repo pass (should be gone)
]

# Files exempt from the whole scan. Keep this list at exactly one entry:
# this file, which necessarily spells out the patterns it looks for.
# Nothing else gets a blanket exemption - use the placeholder allowances
# below instead, so an exemption stays scoped to one known-safe value.
ALLOW = [
    "scripts/privacy-scan.py",
]

# --- Pattern detectors ----------------------------------------------------

# Manifestly fictional values that documentation is expected to contain.
PLACEHOLDER_DOMAINS = {
    "example.com",
    "example.org",
    "example.net",
    "exemple.com",
    "exemple.fr",
    "exemple.org",
    "localhost",
    "test.invalid",
}

# Placeholder account names in a "C:\Users\<...>" path.
PLACEHOLDER_USERS = {
    "...",
    "user",
    "users",
    "username",
    "youruser",
    "your-user",
    "public",
    "default",
    "all users",
    "administrator",
    "runner",      # GitHub-hosted Windows runners
}

# "C:\Users\name" - also matches the doubled backslashes of a JSON or C#
# string literal ("C:\\Users\\name") and the forward-slash spelling that Python
# and JSON configs use ("C:/Users/name"). The leading lookbehind keeps the
# drive letter from matching the tail of a URL scheme ("https://Users/...").
WINDOWS_USER_PATH_RE = re.compile(
    r"(?<![A-Za-z0-9])[A-Za-z]:[\\/]{1,2}Users[\\/]{1,2}([^\\/\s\"'<>|:*?,;)\]}]+)",
    re.IGNORECASE,
)

# "\\server\share" - the single-backslash (verbatim / PowerShell) form only.
# An escaped "\\\\server\\share" inside a C# or JSON string literal is not
# matched; that is a deliberate trade-off against false positives.
UNC_PATH_RE = re.compile(r"(?<![\\\w])\\\\[A-Za-z0-9._-]{2,}\\[A-Za-z0-9._$-]+")

EMAIL_RE = re.compile(r"[A-Za-z0-9._%+-]+@([A-Za-z0-9.-]+\.[A-Za-z]{2,})")

SECRET_RES = [
    ("openai-key", re.compile(r"\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}")),
    ("github-token", re.compile(r"\bgh[pousr]_[A-Za-z0-9]{30,}")),
    ("google-api-key", re.compile(r"\bAIza[0-9A-Za-z_-]{35}\b")),
    ("private-key-block",
     re.compile(r"-----BEGIN (?:RSA |DSA |EC |OPENSSH |PGP )?PRIVATE KEY-----")),
]


def is_placeholder_user(name: str) -> bool:
    """True for a manifestly fictional account name: 'C:\\Users\\...',
    'C:\\Users\\<you>', 'C:\\Users\\%USERNAME%'."""
    n = name.strip().lower()
    if not n:
        return True
    if n in PLACEHOLDER_USERS or n.startswith("..."):
        return True
    return n[0] in "<%${"


def scan_line_patterns(line: str) -> list[tuple[str, str]]:
    """Return (label, matched text) for every non-placeholder pattern hit."""
    hits: list[tuple[str, str]] = []
    for m in WINDOWS_USER_PATH_RE.finditer(line):
        if not is_placeholder_user(m.group(1)):
            hits.append(("windows-user-path", m.group(0)))
    for m in UNC_PATH_RE.finditer(line):
        hits.append(("unc-path", m.group(0)))
    for m in EMAIL_RE.finditer(line):
        if m.group(1).lower() not in PLACEHOLDER_DOMAINS:
            hits.append(("email", m.group(0)))
    for label, regex in SECRET_RES:
        for m in regex.finditer(line):
            hits.append((label, m.group(0)))
    return hits


def is_allowed(rel_path: str) -> bool:
    return any(Path(rel_path).match(p) for p in ALLOW)


def iter_scannable_files():
    """Yield (path, repo-relative posix path) for every published text file."""
    seen: set[str] = set()
    for pattern in SCAN_GLOBS:
        for p in sorted(ROOT.glob(pattern)):
            if not p.is_file():
                continue
            parts = p.relative_to(ROOT).parts
            if any(part in EXCLUDED_DIRS for part in parts[:-1]):
                continue
            if p.suffix.lower() not in TEXT_SUFFIXES and p.name not in TEXT_FILENAMES:
                continue
            rel = "/".join(parts)
            if rel in seen or is_allowed(rel):
                continue
            seen.add(rel)
            yield p, rel


def main() -> int:
    findings: list[tuple[str, int, str, str]] = []
    scanned = 0

    for p, rel in iter_scannable_files():
        try:
            text = p.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        scanned += 1
        for lineno, line in enumerate(text.splitlines(), 1):
            excerpt = None
            for marker in BAD_MARKERS:
                if marker in line:
                    if excerpt is None:
                        excerpt = line.strip()[:117]
                    findings.append((rel, lineno, "marker:" + marker, excerpt))
            for label, matched in scan_line_patterns(line):
                findings.append((rel, lineno, label, matched[:117]))

    if not findings:
        print("[OK] privacy scan: " + str(scanned) + " files, no leak found.")
        return 0

    print("[FAIL] privacy scan: " + str(len(findings)) + " contaminated line(s) found.")
    by_path: dict[str, list[tuple[int, str, str]]] = {}
    for path, line, label, excerpt in findings:
        by_path.setdefault(path, []).append((line, label, excerpt))
    for path, items in sorted(by_path.items()):
        print("\n  " + path + "  (" + str(len(items)) + " hits)")
        for line, label, excerpt in items[:5]:
            # ASCII-safe to avoid cp1252 issues in Windows CI
            safe = excerpt.encode("ascii", errors="replace").decode("ascii")
            print("    L" + str(line).rjust(5) + "  [" + label + "]  " + safe)
        if len(items) > 5:
            print("    ... and " + str(len(items) - 5) + " more")
    print("\nAction: strip the contaminated content before committing.")
    print("        Personal paths belong in an environment variable")
    print("        (see TOPSOLID_HELP_MD_DIR / TOPSOLID_MCP_EXE), not in a file.")
    print("        See scripts/privacy-scan.py for the markers and patterns.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
