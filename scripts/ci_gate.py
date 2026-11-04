#!/usr/bin/env python3
"""ci_gate.py — one-command reproducible quality gate for topsolid-automation-mcp.

Problem this solves: the documented pre-commit gate (`make check`) requires GNU
make, which is not available on stock Windows machines, and the individual
gates (py_compile, unit tests, privacy, recipes) were scattered across Makefile
targets, CI steps and tribal knowledge. Result: "green on CI, unverifiable
locally" drift (see T-1205/T-1175 audit, 2026-11-05: a DNS-sensitive unit test
was red on dev machines for weeks while CI stayed green, unnoticed).

This runner is the single orchestration point. It delegates to the existing
scripts (no check logic is duplicated) and mirrors the CI job order:

  suites (default: all, in this order)
    py-compile  byte-compile every .py under scripts/ into a temp dir
    recipes      scripts/check-recipes.py
    privacy      scripts/privacy-scan.py
    tests        dotnet test on the 3 source-linked unit-test projects
                 (net48, no TopSolid assemblies -> safe without TopSolid)
    graph        dotnet restore + build graph/TopSolidApiGraph.sln

Usage:
  python scripts/ci_gate.py                     # everything
  python scripts/ci_gate.py --only py-compile,tests
  python scripts/ci_gate.py --skip tests,graph  # machine without .NET SDK
  python scripts/ci_gate.py --list

Exit code is 0 only when every selected gate is green. CI (build.yml) calls the
same suites, so `python scripts/ci_gate.py` locally == the Build workflow.

stdlib only, Python 3.9+ (no pip install needed). Works on Windows (native
python.exe) and POSIX; `make ci-local` and .github/workflows/build.yml delegate
to this file (D-068: one definition, three entry points).
"""

import argparse
import os
import py_compile
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

TEST_PROJECTS = [
    "server/tests/TopSolidMcp.Http.Tests/TopSolidMcp.Http.Tests.csproj",
    "server/tests/TopSolidMcpServer.Tests/TopSolidMcpServer.Tests.csproj",
    "server/tests/TopSolidMcp.UpdateCheck.Tests/TopSolidMcp.UpdateCheck.Tests.csproj",
]

GRAPH_SOLUTION = "graph/TopSolidApiGraph.sln"

ALL_SUITES = ["py-compile", "recipes", "privacy", "tests", "graph"]


def _run(cmd, cwd=REPO_ROOT):
    """Run a command, stream output live, return (exit_code, elapsed_seconds)."""
    start = time.time()
    proc = subprocess.run(cmd, cwd=str(cwd))
    return proc.returncode, time.time() - start


def gate_py_compile():
    """Byte-compile every .py under scripts/ into a throwaway dir.

    Catches syntax errors and OneDrive dehydrated placeholders before they
    ship (regression seen 2026-08: lora-pipeline.py was a cloud-only stub).
    """
    scripts_dir = REPO_ROOT / "scripts"
    files = sorted(
        p for p in scripts_dir.rglob("*.py")
        if "__pycache__" not in p.parts
    )
    if not files:
        print("py-compile: no .py file found under scripts/ (unexpected)")
        return 1, 0.0, 0
    failures = []
    start = time.time()
    with tempfile.TemporaryDirectory(prefix="tsmcp_pyc_") as tmp:
        for p in files:
            rel = p.relative_to(scripts_dir)
            cfile = Path(tmp) / (str(rel).replace(os.sep, "_") + ".pyc")
            try:
                py_compile.compile(str(p), cfile=str(cfile), doraise=True)
            except py_compile.PyCompileError as exc:
                failures.append(f"{rel}: {exc}")
    elapsed = time.time() - start
    print(f"py-compile: {len(files)} files")
    for f in failures:
        print(f"  FAIL {f}")
    return (1 if failures else 0), elapsed, len(files)


def gate_script(name, script):
    """Delegate to an existing gate script; its logic stays in one place."""
    print(f"{name}: python {script}")
    code, elapsed = _run([sys.executable, str(REPO_ROOT / script)])
    return code, elapsed, 1


def gate_tests():
    """dotnet test on the source-linked unit projects (no TopSolid needed)."""
    if shutil.which("dotnet") is None:
        print("tests: FAIL — 'dotnet' not found on PATH (use --skip tests,graph)")
        return 1, 0.0, 0
    total_code = 0
    elapsed = 0.0
    count = 0
    for csproj in TEST_PROJECTS:
        print(f"tests: dotnet test {csproj}")
        code, t = _run(["dotnet", "test", csproj, "-c", "Release", "--nologo"])
        total_code = total_code or code
        elapsed += t
        count += 1
    return total_code, elapsed, count


def gate_graph():
    """Same steps the CI 'Build graph/' step runs: restore then build."""
    if shutil.which("dotnet") is None:
        print("graph: FAIL — 'dotnet' not found on PATH (use --skip tests,graph)")
        return 1, 0.0, 0
    print(f"graph: dotnet restore {GRAPH_SOLUTION}")
    code, t1 = _run(["dotnet", "restore", GRAPH_SOLUTION])
    if code != 0:
        return code, t1, 1
    print(f"graph: dotnet build {GRAPH_SOLUTION} -c Release")
    code, t2 = _run(["dotnet", "build", GRAPH_SOLUTION, "-c", "Release", "--no-restore"])
    return code, t1 + t2, 1


GATES = {
    "py-compile": gate_py_compile,
    "recipes": lambda: gate_script("recipes", "scripts/check-recipes.py"),
    "privacy": lambda: gate_script("privacy", "scripts/privacy-scan.py"),
    "tests": gate_tests,
    "graph": gate_graph,
}


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Reproducible local quality gate (mirrors CI).")
    parser.add_argument("--only", metavar="A,B",
                        help="comma-separated suites to run (default: all)")
    parser.add_argument("--skip", metavar="A,B", default="",
                        help="comma-separated suites to skip from the default set")
    parser.add_argument("--list", action="store_true",
                        help="list available suites and exit")
    args = parser.parse_args(argv)

    if args.list:
        print("suites: " + ", ".join(ALL_SUITES))
        return 0

    if args.only:
        selected = [s.strip() for s in args.only.split(",") if s.strip()]
    else:
        selected = list(ALL_SUITES)
    skip = {s.strip() for s in args.skip.split(",") if s.strip()}
    selected = [s for s in selected if s not in skip]

    unknown = [s for s in selected if s not in GATES]
    if unknown:
        print(f"unknown suite(s): {', '.join(unknown)} "
              f"(valid: {', '.join(ALL_SUITES)})")
        return 2

    results = []
    for suite in selected:
        print(f"\n=== {suite} " + "=" * (50 - len(suite)))
        code, elapsed, _ = GATES[suite]()
        status = "OK  " if code == 0 else "FAIL"
        print(f"--- {suite}: {status} ({elapsed:.1f}s)")
        results.append((suite, code, elapsed))

    print("\n================ ci_gate verdict ================")
    worst = 0
    for suite, code, elapsed in results:
        status = "OK  " if code == 0 else "FAIL"
        worst = worst or code
        print(f"  {status} {suite:<12} {elapsed:7.1f}s")
    print(f"exit code: {worst}")
    return worst


if __name__ == "__main__":
    sys.exit(main())
