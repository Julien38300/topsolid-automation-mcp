#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Recipe catalogue checker for server/src/Tools/RecipeTool.cs.

CLAUDE.md requires `make check` to pass before RecipeTool.cs is committed.
This is the recipe half of that gate (the other half is scripts/privacy-scan.py).
Nothing here compiles C#: the machine that builds the server needs TopSolid
installed, so the checks are textual on purpose and must stay cheap enough to run
on a GitHub runner.

Checks, in order:

1. The catalogue is found and parsed. A zero-entry parse is a failure, not a pass:
   it means the file drifted away from the shape this script understands.
2. Every dictionary key in the catalogue uses one of the R() / RW() / RD()
   factories. An entry built any other way is not classified and is reported.
3. Recipe names are unique (a duplicate key throws at static-initialisation time
   and takes the whole server down on the first tool call).
4. A recipe whose C# body calls a TopSolid write API is declared WritePdm
   (R() -> RW()) or WriteDisk (R() -> RD()), never Read. A Read recipe that
   writes bypasses both the read-only guard and the modification transaction.
5. Recipe bodies use no C# 6 string interpolation. Recipe code is compiled by
   CSharpCodeProvider as C# 5, where $"..." is a syntax error at run time. This
   one is a heuristic, so it is reported as a warning and never blocks.

Checks 1 to 4 are failures. Warnings are printed but do not change the exit code.
Exit code 0 = clean. Non-zero = at least one FAIL; CI and `make check` block.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_RECIPE_FILE = ROOT / "server" / "src" / "Tools" / "RecipeTool.cs"

# Start of the catalogue. Everything checked below lives between this declaration
# and the first factory method definition, which the catalogue is always followed
# by. Bounding the region this way keeps unrelated dictionary literals elsewhere
# in the file (JSON schema fragments, lookup tables) out of the checks.
CATALOGUE_START = "Recipes = new Dictionary<string, RecipeEntry>"
CATALOGUE_END = re.compile(r"^[ \t]*private static RecipeEntry\b", re.M)

# A catalogue entry: { "recipe_name", R(...) / RW(...) / RD(...)
# Names may contain digits (read_3d_points, select_3d_point).
ENTRY = re.compile(r'\{\s*"([a-z][a-z0-9_]*)"\s*,\s*(R|RW|RD)\(')

# Anything shaped like a dictionary entry, whatever it is built from. Used to
# catch entries that do not go through the three factories.
ANY_ENTRY = re.compile(r'^[ \t]*\{\s*"([A-Za-z0-9_]+)"\s*,', re.M)

FACTORY_MODE = {"R": "Read", "RW": "WritePdm", "RD": "WriteDisk"}

# Verbs that mean "this recipe changes something". Matched on the TopSolid
# facades only (TopSolidHost, TopSolidDesignHost, TopSolidDraftingHost), so
# ordinary StringBuilder / List calls are not flagged.
# "Open" and "Close" are deliberately absent: opening a document is how several
# read-only recipes reach the data they report on.
WRITE_VERBS = (
    "Set", "Create", "Delete", "Save", "Export", "Import", "Write",
    "Add", "Remove", "Insert", "Rename", "Move", "Copy",
    "CheckIn", "CheckOut", "Print", "Start", "End", "Apply", "Update", "Modify",
    "Rebuild", "Invoke", "Execute", "Activate", "Deactivate", "Assign",
    "Attach", "Detach", "Replace", "Clear", "Unfold", "Generate", "Build", "Commit",
)
WRITE_CALL = re.compile(
    r"TopSolid\w*Host\.(?:\w+\.)*(?:" + "|".join(WRITE_VERBS) + r")\w*\s*\("
)

# C# 6 interpolation. Recipe bodies are compiled as C# 5 by CSharpCodeProvider,
# where $"..." is a run-time syntax error. Recipe code lives inside C# string
# literals, so an interpolated string appears here as $\" - with the quote
# escaped. The lookbehind drops the common false positive name.Contains("$"),
# which reads as \"$\". A literal $ at the very end of a recipe string would
# still match, which is why this one is reported as a warning, not a failure.
INTERPOLATION = re.compile(r'(?<!\\")\$\\"')


class Entry(object):
    def __init__(self, name, factory, body, line):
        self.name = name
        self.factory = factory
        self.body = body
        self.line = line

    @property
    def mode(self):
        return FACTORY_MODE[self.factory]


def ascii_safe(text):
    return text.encode("ascii", errors="replace").decode("ascii")


def line_of(text, index):
    return text.count("\n", 0, index) + 1


def find_catalogue(source):
    """Returns (region_text, region_offset) or (None, 0) when not found."""
    start = source.find(CATALOGUE_START)
    if start < 0:
        return None, 0
    tail = CATALOGUE_END.search(source, start)
    end = tail.start() if tail else len(source)
    return source[start:end], start


def parse_entries(region, offset, source):
    matches = list(ENTRY.finditer(region))
    entries = []
    for i, match in enumerate(matches):
        body_end = matches[i + 1].start() if i + 1 < len(matches) else len(region)
        entries.append(Entry(
            name=match.group(1),
            factory=match.group(2),
            body=region[match.start():body_end],
            line=line_of(source, offset + match.start()),
        ))
    return entries


def main(argv):
    recipe_file = Path(argv[1]) if len(argv) > 1 else DEFAULT_RECIPE_FILE
    print("Recipe check: " + str(recipe_file))

    if not recipe_file.is_file():
        print("[FAIL] recipe file not found.")
        return 1

    source = recipe_file.read_text(encoding="utf-8")
    region, offset = find_catalogue(source)
    if region is None:
        print("[FAIL] catalogue not found: no '" + CATALOGUE_START + "' in the file.")
        print("       The checker must be updated to match the new layout.")
        return 1

    entries = parse_entries(region, offset, source)
    failures = []
    warnings = []

    # 1. Something must have been parsed.
    if not entries:
        failures.append("no recipe parsed - the catalogue layout changed and this "
                        "checker no longer sees anything. Fix the checker.")

    # 2. Entries that do not use R() / RW() / RD().
    known = set(entry.name for entry in entries)
    for match in ANY_ENTRY.finditer(region):
        name = match.group(1)
        if name not in known:
            failures.append(
                "L" + str(line_of(source, offset + match.start())) + "  '" + name +
                "' is a catalogue entry but does not use R() / RW() / RD().")

    # 3. Duplicate names.
    seen = {}
    for entry in entries:
        if entry.name in seen:
            failures.append(
                "L" + str(entry.line) + "  duplicate recipe name '" + entry.name +
                "' (first defined at L" + str(seen[entry.name]) + ").")
        else:
            seen[entry.name] = entry.line

    # 4. Read recipes that write, and write recipes that do not.
    for entry in entries:
        hits = sorted(set(m.group(0)[:-1].strip() for m in WRITE_CALL.finditer(entry.body)))
        if entry.factory == "R" and hits:
            failures.append(
                "L" + str(entry.line) + "  '" + entry.name + "' is declared Read (R) but "
                "calls " + ", ".join(hits[:3]) +
                ("..." if len(hits) > 3 else "") +
                " - use RW() for a PDM write or RD() for a disk write.")
        elif entry.factory in ("RW", "RD") and not hits:
            warnings.append(
                "L" + str(entry.line) + "  '" + entry.name + "' is declared " +
                entry.mode + " but no TopSolid write call was detected.")

    # 5. C# 6 interpolation in recipe bodies (heuristic, hence a warning).
    for entry in entries:
        if INTERPOLATION.search(entry.body):
            warnings.append(
                "L" + str(entry.line) + "  '" + entry.name + "' looks like it uses "
                "$\"...\" interpolation; recipe code is compiled as C# 5.")

    # --- Report -------------------------------------------------------------
    counts = {"Read": 0, "WritePdm": 0, "WriteDisk": 0}
    for entry in entries:
        counts[entry.mode] += 1
    print("  recipes    : " + str(len(entries)))
    print("  Read       : " + str(counts["Read"]))
    print("  WritePdm   : " + str(counts["WritePdm"]))
    print("  WriteDisk  : " + str(counts["WriteDisk"]))

    # ASCII-safe output, so a cp1252 console in CI cannot turn a report into a
    # UnicodeEncodeError traceback.
    for warning in warnings:
        print("  [WARN] " + ascii_safe(warning))

    if failures:
        print("[FAIL] recipe check: " + str(len(failures)) + " problem(s).")
        for failure in failures:
            print("  " + ascii_safe(failure))
        return 1

    print("[OK] recipe check: " + str(len(entries)) + " recipes, no problem found.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
