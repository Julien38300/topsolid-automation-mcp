#!/usr/bin/env python3
"""T-1066 / étape 7 : capturer le payload [AVAILABLE_TOOLS] RÉEL du serveur MCP.

Se connecte au TopSolidMcpServer compilé en stdio (profil small ou défaut),
récupère la réponse exacte de `tools/list`, convertit les outils MCP au format
Ollama ({"type":"function","function":{...}}) — c'est ce que `$.Tools` contient
quand le TEMPLATE du Modelfile l'injecte — et écrit le blob compact dans
data/available-tools.json. train-lora.py lit ce fichier pour rendre les
échantillons d'entraînement identiques au prompt servi.

Usage :
  python scripts/capture-available-tools.py            # profil small
  python scripts/capture-available-tools.py --profile full
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BIN = REPO / "server" / "src" / "bin" / "Release" / "net48" / "TopSolidMcpServer.exe"
OUT = REPO / "data" / "available-tools.json"


def mcp_to_ollama(tools):
    """Conversion MCP -> format Ollama (sérialisation de `$.Tools` côté service)."""
    oll = []
    for t in tools:
        oll.append({
            "type": "function",
            "function": {
                "name": t["name"],
                "description": t.get("description", ""),
                "parameters": t.get("inputSchema", {"type": "object"}),
            },
        })
    return oll


def capture_tools_list(profile):
    env = dict(os.environ, TOPSOLID_MCP_TRANSPORT="stdio",
               TOPSOLID_MCP_PROFILE=profile)
    init = {"jsonrpc": "2.0", "id": 1, "method": "initialize",
            "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                       "clientInfo": {"name": "t1066-capture", "version": "0"}}}
    notif = {"jsonrpc": "2.0", "method": "notifications/initialized"}
    lst = {"jsonrpc": "2.0", "id": 2, "method": "tools/list"}
    p = subprocess.Popen([str(BIN)], stdin=subprocess.PIPE,
                         stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                         env=env, text=True, encoding="utf-8")
    try:
        for obj in (init, notif, lst):
            p.stdin.write(json.dumps(obj) + "\n")
        p.stdin.flush()
        for _ in range(20):
            line = p.stdout.readline()
            if not line:
                break
            line = line.strip()
            if not line:
                continue
            try:
                msg = json.loads(line)
            except Exception:
                continue
            if msg.get("id") == 2 and "result" in msg:
                return msg["result"]["tools"]
    finally:
        p.kill()
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--profile", choices=["small", "full"], default="small")
    args = ap.parse_args()
    tools = capture_tools_list(args.profile)
    if not tools:
        print("NO tools/list response — serveur absent ou build manquant")
        return 1
    blob = json.dumps(mcp_to_ollama(tools), ensure_ascii=False,
                      separators=(",", ":"))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(blob, encoding="utf-8")
    names = sorted(t["name"] for t in tools)
    print(f"profile={args.profile} tools={len(tools)} chars={len(blob)}")
    print("names:", ", ".join(names))
    print(f"written: {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
