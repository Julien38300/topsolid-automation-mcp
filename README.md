# topsolid-automation-mcp

**Community Model Context Protocol server for TopSolid® 7** — bring any MCP-compatible AI agent (Claude, ChatGPT-MCP clients, Cursor, Windsurf, JetBrains, VS Code + Copilot, OpenClaw, etc.) into your TopSolid workflow.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Docs](https://img.shields.io/badge/docs-vitepress-brightgreen)](https://julien38300.github.io/topsolid-automation-mcp/)
[![Release](https://img.shields.io/github/v/release/Julien38300/topsolid-automation-mcp)](https://github.com/Julien38300/topsolid-automation-mcp/releases/latest)
[![CI](https://img.shields.io/github/actions/workflow/status/Julien38300/topsolid-automation-mcp/privacy-scan.yml?branch=main&label=privacy-scan)](https://github.com/Julien38300/topsolid-automation-mcp/actions)

> **Community project.** TopSolid® is a registered trademark of [TOPSOLID SAS](https://www.topsolid.com/). This repository is an **independent, community-maintained** MCP server. It is not endorsed by, sponsored by, or affiliated with TOPSOLID SAS. It wraps the publicly documented TopSolid Automation API and indexes the publicly shipped help documentation. A valid TopSolid license is required to run it against a live TopSolid instance.

## What is this?

An MCP server written in C# (.NET Framework 4.8) that exposes the TopSolid 7 Automation API as a set of 13 tools any MCP client can call. On top of the raw API, it ships with:

- **132 pre-built recipes** — curated C# snippets for the most common CAD/PDM operations (read mass/volume, set designation, export STEP/DXF/PDF, activate BOM rows, detect drafting scale, ...), browsable with `topsolid_list_recipes`.
- A **4119-edge type graph** of the API (1728 methods, 242 types), queryable by Dijkstra / BFS to discover method chains between any two types.
- A **5809-page help index** (EN + FR) in SQLite FTS5 — ask your agent *"how does the sheet-metal unfolding command work?"* and get the official help excerpt.
- A **2428-command catalog** — look up any ribbon/menu command by keyword, get its FullName ready to invoke.
- A **dry-run compile check** — the script is compiled against the TopSolid assemblies without being run, so hallucinated APIs and syntax errors surface before execution. It uses `CSharpCodeProvider` (the `csc` compiler bundled with the .NET Framework), **not** Roslyn: generated snippets are therefore compiled as **C# 5**, which is why string interpolation (`$"..."`) is rejected everywhere in generated code.
- Corpus search over local user-provided C# examples (paths configured locally, never shipped).

Plus an optional **HTTP/SSE bridge** for remote clients (claude.ai web, mobile apps, server-side agents).

## Quick start

### Prerequisites

- Windows 10/11
- TopSolid 7.15+ (tested on 7.20 / 7.21)
- An MCP client: Claude Code, Claude Desktop, Cursor, Windsurf, VS Code + Copilot...

### Install

1. Enable remote access in TopSolid: **Tools > Options > General > Automation** → check "Manage remote access", port 8090, restart TopSolid.
2. Download the [latest release](https://github.com/Julien38300/topsolid-automation-mcp/releases/latest) (`TopSolidMcpServer-vX.Y.Z.zip`) and unzip to e.g. `C:\TopSolidMCP\`.
3. Register with your client. For Claude Code CLI:

   ```powershell
   claude mcp add --scope user topsolid C:\TopSolidMCP\TopSolidMcpServer.exe
   ```

Full per-client setup (Claude Desktop, Cursor, Windsurf, JetBrains, VS Code, Antigravity, Continue, OpenClaw...) is in the [integration guide](https://julien38300.github.io/topsolid-automation-mcp/guide/integration).

### First call

In your AI assistant, ask:
> *Read the designation of the open TopSolid document.*

The agent picks `topsolid_run_recipe` with recipe `read_designation` and returns the result.

## The 13 MCP tools

| Tool | Purpose | Needs TopSolid running |
|---|---|---|
| `topsolid_run_recipe` | Run one of 132 pre-built recipes | yes |
| `topsolid_list_recipes` | Browse the recipe catalog, filtered by category or keyword | no |
| `topsolid_api_help` | Search 1728 API methods (72 FR/EN synonyms) | no |
| `topsolid_find_path` | Shortest method chain between two types (Dijkstra) | no |
| `topsolid_explore_paths` | Multi-path BFS between two types | no |
| `topsolid_get_state` | Active document, project, connection status | yes |
| `topsolid_execute_script` | Compile and run arbitrary C# in the server process, outside a TopSolid modification transaction | yes |
| `topsolid_modify_script` | Compile and run arbitrary C# inside a modification transaction (Pattern D auto-wrapped) | yes |
| `topsolid_get_recipe` | Return the C# source of a recipe | no |
| `topsolid_compile` | Dry-run compile against the TopSolid assemblies (`CSharpCodeProvider`, C# 5) | no |
| `topsolid_search_examples` | Search user-local private corpora of C# samples | no |
| `topsolid_search_help` | FTS5 search over 5809 help pages (EN+FR) | no |
| `topsolid_search_commands` | Lookup UI commands in the 2428-command catalog | no |

### Security: do not auto-approve the script tools

`topsolid_execute_script` and `topsolid_modify_script` are **not** sandboxed, and "read-only" in this project has only ever meant *outside a TopSolid modification transaction* — it is not a permission level.

Both tools compile the C# they are handed and run it **in-process, fully trusted**, on your workstation. `System.IO` is available (export recipes need it), so a script can read and write files anywhere your user account can. A deny-list rejects a handful of symbols before compilation (`System.Diagnostics.Process`, `System.Net`, `System.Reflection`, `DllImport`, `File.Delete`, `Directory.Delete`, registry access, `Environment.Exit`); it is a guardrail against an LLM wandering off, not a security boundary, and `TOPSOLID_MCP_ALLOW_UNSAFE=1` removes it entirely.

Practical consequences:

- **Do not add `topsolid_execute_script` or `topsolid_modify_script` to the auto-approved / always-allow tool list of your MCP client.** Read each script before you let it run.
- Run the server with `--read-only` (or `TOPSOLID_MCP_READ_ONLY=1`) when you only need to query: `topsolid_modify_script` is then not registered at all, and recipes run in read-only mode.
- Do not expose the HTTP bridge publicly without authentication — see the [bridge guide](https://julien38300.github.io/topsolid-automation-mcp/guide/bridge-http).

## Data sources & attribution

All the knowledge this server exposes comes from **publicly available** TopSolid material:

- **API graph** (4119 edges, 1728 methods) — extracted by reflection from the TopSolid Automation `.dll` assemblies shipped with every TopSolid installation, cross-referenced with the official API reference at [help.topsolid.com](https://help.topsolid.com/).
- **Help index** (5809 pages, EN + FR) — converted to Markdown from the publicly shipped help site at [help.topsolid.com](https://help.topsolid.com/).
- **UI commands catalog** (2428 commands) — parsed from those same help pages (files ending in `*Command.md`).
- **Recipes** (132 C# snippets) — hand-written for this project, referring back to the public help and the graph for each API call.

No proprietary TopSolid SDK sample code, no customer project, and no identified individual's private code is included in anything that ships with this repo. Private corpora support (`topsolid_search_examples`) is opt-in via environment variables on the user's own machine — nothing is ever bundled.

## Why this exists

TopSolid ships a powerful .NET Automation API, but it is hard to discover: 1728 methods across 46 interfaces, ~10-20% of the product surface is only reachable via ribbon/menu commands, and half the "how do I..." answers are in help pages no one reads. This server collapses that discovery cost for an AI agent — and for a human developer who treats the MCP as a knowledge base for writing standalone TopSolid Automation apps.

## Repository layout

```
topsolid-automation-mcp/
├── server/        TopSolid MCP Server (.NET 4.8, stdio JSON-RPC)
│   ├── src/       Program.cs, Protocol/, Tools/, Utils/
│   ├── data/      Data shipped next to the .exe: graph.json, help.db,
│   │              commands-catalog.json, recipe-list.txt, recipes.md
│   ├── scripts/   build-release.ps1, update.ps1, sync-topsolid-api.py
│   └── models/    Ollama Modelfile for the local code sub-agent
├── graph/         API graph builder (.NET 4.8, reflection-based)
├── bridge/        HTTP/SSE bridge for remote clients (Node, mcp-proxy wrapper)
├── scripts/       Graph enrichment, help index, commands catalog, privacy scan (Python)
├── data/          Build-time artefacts: graph.json, api-index.json,
│                  recipe-name-mapping-fr-en.json, recipes.md
├── docs/          VitePress documentation site
├── tests/         Automated test harness (C# runner + PowerShell scripts)
└── skills/        Optional skill package for MCP-aware agents
```

`server/data/` is what gets packaged into the release zip and read at runtime (`help.db` in particular lives there, not in the top-level `data/`).

## Contributing

Read [CONTRIBUTING.md](./CONTRIBUTING.md). Short version: pick up a [good-first-issue](https://github.com/Julien38300/topsolid-automation-mcp/labels/good%20first%20issue), add a recipe to `server/src/Tools/RecipeTool.cs`, or improve the docs. Every PR helps.

All interactions are governed by our [Code of Conduct](./CODE_OF_CONDUCT.md).

## License

[MIT](./LICENSE). Use commercially, fork, modify freely. TopSolid itself is proprietary — you need your own license from TOPSOLID SAS to run this against a live TopSolid instance.
