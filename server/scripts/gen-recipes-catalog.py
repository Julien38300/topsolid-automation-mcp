"""Regenerate server/data/recipes.md from the recipes the server actually runs.

recipes.md is a reference for people and agents; nothing in the server reads it. Hand-kept,
it had drifted from RecipeTool.cs (99 stale bodies, 12 wrong modes, 3 recipes missing on
2026-11-09). This script asks a built server for every recipe of recipe-list.txt with
topsolid_get_recipe (stdio transport: no API key, no TopSolid needed) and rewrites the file.

Usage:
    python server/scripts/gen-recipes-catalog.py server/src/bin/Debug/net48/TopSolidMcpServer.exe
    python server/scripts/gen-recipes-catalog.py <exe> --check     (exit 1 when recipes.md is stale)
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

SERVER_DIR = Path(__file__).resolve().parent.parent
CATALOG = SERVER_DIR / "data" / "recipes.md"
RECIPE_LIST = SERVER_DIR / "data" / "recipe-list.txt"
HEADER = "# TopSolid MCP Recipes Catalog\n"
MODES = {"[READ]": "R", "[WRITE-PDM]": "RW", "[WRITE-DISK]": "RD", "[WRITE-PDM-NO-TRX]": "RN"}


class Server(object):
    def __init__(self, exe: str):
        env = dict(os.environ)
        env.setdefault("TOPSOLID_MCP_HTTP_PORT", "18519")  # never collide with a running bridge
        self.proc = subprocess.Popen([exe, "--no-tray"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                     stderr=subprocess.DEVNULL, env=env, cwd=os.path.dirname(os.path.abspath(exe)))
        self.next_id = 0
        self.call("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                 "clientInfo": {"name": "gen-recipes-catalog", "version": "1"}})

    def call(self, method: str, params: dict) -> dict:
        self.next_id += 1
        msg = json.dumps({"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": params})
        self.proc.stdin.write((msg + "\n").encode("utf-8"))
        self.proc.stdin.flush()
        while True:
            line = self.proc.stdout.readline()
            if not line:
                raise RuntimeError("the server closed stdout")
            try:
                res = json.loads(line.decode("utf-8"))
            except ValueError:
                continue
            if res.get("id") == self.next_id:
                return res

    def recipe(self, name: str) -> str:
        res = self.call("tools/call", {"name": "topsolid_get_recipe", "arguments": {"recipe": name}})
        if "error" in res:
            raise RuntimeError("%s: %s" % (name, res["error"]))
        return "\n".join(c.get("text", "") for c in res["result"]["content"])

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=10)
        except Exception:
            self.proc.kill()


def block(name: str, text: str) -> str:
    mode = re.search(r"^Mode:\s+(\[[A-Z-]+\])", text, re.M)
    desc = re.search(r"^Description: (.*)$", text, re.M)
    code = re.search(r"```csharp\n(.*?)\n```", text.replace("\r\n", "\n"), re.S)
    if not (mode and desc and code) or mode.group(1) not in MODES:
        raise RuntimeError("unexpected topsolid_get_recipe output for %s:\n%s" % (name, text[:400]))
    return "## %s\nPattern: %s\nDescription: %s\n\n```csharp\n%s\n```\n" % (
        name, MODES[mode.group(1)], desc.group(1).strip(), code.group(1).strip("\n"))


def main(argv: list) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("exe", help="built TopSolidMcpServer.exe")
    ap.add_argument("--check", action="store_true", help="only report whether recipes.md is up to date")
    args = ap.parse_args(argv)

    names = [l.strip() for l in RECIPE_LIST.read_text(encoding="utf-8").splitlines() if l.strip()]
    server = Server(args.exe)
    try:
        blocks = [block(n, server.recipe(n)) for n in names]
    finally:
        server.close()

    text = HEADER + "\n" + "\n".join(blocks)
    current = CATALOG.read_text(encoding="utf-8").replace("\r\n", "\n") if CATALOG.exists() else ""
    if args.check:
        if current == text:
            print("[OK] recipes.md matches the %d recipes of the server." % len(names))
            return 0
        print("[STALE] recipes.md differs from the server: run without --check to regenerate it.")
        return 1

    CATALOG.write_bytes(text.encode("utf-8"))
    print("recipes.md: %d recipes written." % len(names))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
