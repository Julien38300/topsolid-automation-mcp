"""Unit tests for scripts/check-recipes.py (pure logic, no C# / no TopSolid).

Covers the T-1175 open item (2026-11-05): dedicated pytest coverage for the
logic of the repo's Python scripts. The regexes and parsing helpers below are
the ones guarding the recipe catalogue of RecipeTool.cs.
"""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "check_recipes", ROOT / "scripts" / "check-recipes.py")
cr = importlib.util.module_from_spec(spec)
sys.modules.setdefault("check_recipes", cr)
spec.loader.exec_module(cr)

CATALOGUE_TEMPLATE = '''using System;
public partial class RecipeTool
{{
    private static readonly Dictionary<string, RecipeEntry> Recipes = new Dictionary<string, RecipeEntry>
        {{
{entries}
        }};

    {end_marker} private static RecipeEntry R(string name) {{ return null; }}
}}
'''


def make_source(entries_lines: str) -> str:
    return CATALOGUE_TEMPLATE.format(
        entries=entries_lines, end_marker="private static RecipeEntry")


def write_recipe_file(tmp_path: Path, entries_lines: str) -> Path:
    path = tmp_path / "RecipeTool.cs"
    path.write_text(make_source(entries_lines), encoding="utf-8")
    return path


class TestCatalogueParsing:

    def test_find_catalogue_bounds_region(self):
        source = make_source('            { "open_document", R(() => 0) },')
        region, offset = cr.find_catalogue(source)
        assert region is not None
        assert cr.CATALOGUE_START in region
        # The region stops at the first factory definition: the trailing
        # `private static RecipeEntry R(string name) { ... }` is outside it.
        assert "return null" not in region
        assert offset == source.find(cr.CATALOGUE_START)

    def test_find_catalogue_missing_returns_none(self):
        assert cr.find_catalogue("no catalogue here") == (None, 0)

    def test_parse_entries_names_and_factories(self):
        region = '{ "read_dims", R(() => 0) }, { "save_part", RW(() => 0) },'
        entries = cr.parse_entries(region, 0, region)
        assert [(e.name, e.factory, e.mode) for e in entries] == [
            ("read_dims", "R", "Read"), ("save_part", "RW", "WritePdm")]

    def test_entry_lines_are_absolute(self):
        entries_lines = ('            { "one", R(() => 0) },\n'
                         '            { "two", RD(() => 0) },')
        source = make_source(entries_lines)
        region, offset = cr.find_catalogue(source)
        entries = cr.parse_entries(region, offset, source)
        assert entries[1].line == entries[0].line + 1


class TestWriteCallDetection:

    def test_read_recipe_calling_write_api_fails(self, tmp_path):
        lines = '            { "evil_read", R(() => TopSolidDesignHost.SetName(null, "x")) },'
        path = write_recipe_file(tmp_path, lines)
        out = cr.main(["prog", str(path)])
        assert isinstance(out, int)
        assert out == 1

    def test_read_recipe_without_write_passes(self, tmp_path):
        lines = '            { "read_dims", R(() => TopSolidHost.GetComponentCount(null)) },'
        path = write_recipe_file(tmp_path, lines)
        assert cr.main(["prog", str(path)]) == 0

    def test_write_recipe_without_write_call_is_warning_not_failure(self, tmp_path):
        lines = '            { "declared_write", RW(() => 0) },'
        path = write_recipe_file(tmp_path, lines)
        # Only a warning -> exit 0.
        assert cr.main(["prog", str(path)]) == 0

    def test_write_call_regex_only_matches_topsolid_facades(self):
        assert cr.WRITE_CALL.search('TopSolidHost.Delete(null)')
        assert cr.WRITE_CALL.search('TopSolidDesignHost.Save(null)')
        assert not cr.WRITE_CALL.search('list.Add("Delete")')
        assert not cr.WRITE_CALL.search('helper.CopyData()')


class TestFailureClasses:

    def test_duplicate_names_fail(self, tmp_path):
        lines = ('            { "dup", R(() => 0) },\n'
                 '            { "dup", R(() => 0) },')
        path = write_recipe_file(tmp_path, lines)
        assert cr.main(["prog", str(path)]) == 1

    def test_non_factory_entry_fails(self, tmp_path):
        lines = '            { "weird", RecipeEntry.New(() => 0) },'
        path = write_recipe_file(tmp_path, lines)
        assert cr.main(["prog", str(path)]) == 1

    def test_missing_file_fails(self, tmp_path):
        assert cr.main(["prog", str(tmp_path / "absent.cs")]) == 1

    def test_csharp6_interpolation_is_warning_only(self, tmp_path):
        body = ('            { "interp", RD(() =>'
                r' part.Export($\"C:\\out.nc1\")) },')
        path = write_recipe_file(tmp_path, body)
        assert cr.main(["prog", str(path)]) == 0
