"""Unit tests for scripts/ci_gate.py — suite selection + py-compile gate logic.

T-1175 open item (2026-11-05): "tests unitaires Python dédiés pour la logique
des scripts". The CI orchestration (only/skip/unknown, verdict worst-code) and
the OneDrive-dehydration guard (byte-compiling a synthetic scripts tree) are
tested here without running dotnet, semgrep or the real repo gates.
"""
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "ci_gate", ROOT / "scripts" / "ci_gate.py")
cig = importlib.util.module_from_spec(spec)
sys.modules.setdefault("ci_gate", cig)
spec.loader.exec_module(cig)


class TestSuiteSelection:

    def test_list_prints_suites(self, capsys):
        assert cig.main(["--list"]) == 0
        assert "py-compile" in capsys.readouterr().out

    def test_unknown_suite_exits_2(self, capsys):
        assert cig.main(["--only", "nope"]) == 2

    def test_only_filters_and_runs_fake_gate(self, capsys, monkeypatch):
        calls = []
        monkeypatch.setitem(cig.GATES, "py-compile",
                            lambda: calls.append("py-compile") or (0, 0.1, 1))
        assert cig.main(["--only", "py-compile"]) == 0
        assert calls == ["py-compile"]

    def test_skip_removes_suites(self, monkeypatch, capsys):
        counts = {"py-compile": 0, "recipes": 0}
        monkeypatch.setitem(cig.GATES, "recipes",
                            lambda: counts.__setitem__("recipes", 1) or (0, 0.0, 1))
        monkeypatch.setitem(cig.GATES, "py-compile",
                            lambda: counts.__setitem__("py-compile", 1) or (0, 0.0, 1))
        assert cig.main(["--only", "py-compile,recipes", "--skip", "recipes"]) == 0
        assert counts == {"py-compile": 1, "recipes": 0}

    def test_verdict_uses_worst_exit_code(self, capsys, monkeypatch):
        order = iter([(0, 0.0, 1), (1, 0.0, 1)])
        monkeypatch.setitem(cig.GATES, "py-compile", lambda: next(order))
        monkeypatch.setitem(cig.GATES, "recipes", lambda: next(order))
        assert cig.main(["--only", "py-compile,recipes"]) == 1


class TestPyCompileGate:

    def _run_gate_on(self, repo_root, capsys):
        monkey_target = cig
        monkey_target.REPO_ROOT = repo_root
        return monkey_target.gate_py_compile()

    def test_green_on_valid_tree(self, tmp_path, capsys, monkeypatch):
        scripts = tmp_path / "scripts"
        scripts.mkdir()
        (scripts / "good.py").write_text("x = 1\n", encoding="utf-8")
        (scripts / "sub").mkdir()
        (scripts / "sub" / "ok.py").write_text("from math import ceil\n",
                                               encoding="utf-8")
        monkeypatch.setattr(cig, "REPO_ROOT", tmp_path)
        code, _elapsed, nfiles = cig.gate_py_compile()
        assert (code, nfiles) == (0, 2)
        assert "py-compile: 2 files" in capsys.readouterr().out

    def test_red_on_broken_syntax(self, tmp_path, capsys, monkeypatch):
        scripts = tmp_path / "scripts"
        scripts.mkdir()
        (scripts / "good.py").write_text("x = 1\n", encoding="utf-8")
        (scripts / "bad.py").write_text("def f(:\n", encoding="utf-8")
        monkeypatch.setattr(cig, "REPO_ROOT", tmp_path)
        code, _elapsed, nfiles = cig.gate_py_compile()
        assert code == 1 and nfiles == 2
        assert "bad.py" in capsys.readouterr().out

    def test_pycache_dirs_are_ignored(self, tmp_path, monkeypatch):
        scripts = tmp_path / "scripts"
        (scripts / "__pycache__").mkdir(parents=True)
        (scripts / "__pycache__" / "junk.py").write_text("def f(:\n",
                                                         encoding="utf-8")
        (scripts / "main.py").write_text("y = 2\n", encoding="utf-8")
        monkeypatch.setattr(cig, "REPO_ROOT", tmp_path)
        code, _elapsed, nfiles = cig.gate_py_compile()
        assert (code, nfiles) == (0, 1)

    def test_empty_tree_fails(self, tmp_path, monkeypatch):
        (tmp_path / "scripts").mkdir()
        monkeypatch.setattr(cig, "REPO_ROOT", tmp_path)
        code, _elapsed, nfiles = cig.gate_py_compile()
        assert code == 1 and nfiles == 0
