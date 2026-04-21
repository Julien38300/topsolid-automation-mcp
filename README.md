# topsolid-automation-mcp

**Community Model Context Protocol server for TopSolid 7** — bring any MCP-compatible AI agent (Claude, ChatGPT-MCP clients, Cursor, Windsurf, JetBrains, VS Code + Copilot, OpenClaw, etc.) into your TopSolid workflow.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![Docs](https://img.shields.io/badge/docs-vitepress-brightgreen)](https://julien38300.github.io/topsolid-automation-mcp/)
[![Release](https://img.shields.io/github/v/release/Julien38300/topsolid-automation-mcp)](https://github.com/Julien38300/topsolid-automation-mcp/releases/latest)
[![CI](https://img.shields.io/github/actions/workflow/status/Julien38300/topsolid-automation-mcp/privacy-scan.yml?branch=main&label=privacy-scan)](https://github.com/Julien38300/topsolid-automation-mcp/actions)

> TopSolid(R) is a registered trademark of [TOPSOLID SAS](https://www.topsolid.com/). This project is independent and not endorsed by or affiliated with TOPSOLID SAS. It wraps the publicly documented TopSolid Automation API.

## What is this?

An MCP server written in C# (.NET Framework 4.8) that exposes the TopSolid 7 Automation API as a set of 13 tools any MCP client can call. On top of the raw API, it ships with:

- **130 pre-built recipes** — curated C# snippets for the most common CAD/PDM operations (read mass/volume, set designation, export STEP/DXF/PDF, activate BOM rows, detect drafting scale, ...).
- A **4119-edge type graph** of the API (1728 methods, 242 types), queryable by Dijkstra / BFS to discover method chains between any two types.
- A **5809-page help index** (EN + FR) in SQLite FTS5 — ask your agent *"how does the sheet-metal unfolding command work?"* and get the official help excerpt.
- A **2428-command catalog** — look up any ribbon/menu command by keyword, get its FullName ready to invoke.
- A **Roslyn-based dry-run compiler** — validate generated C# before executing it.
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
| `topsolid_run_recipe` | Run one of 130 pre-built recipes | yes |
| `topsolid_api_help` | Search 1728 API methods (52 FR/EN synonyms) | no |
| `topsolid_find_path` | Shortest method chain between two types (Dijkstra) | no |
| `topsolid_explore_paths` | Multi-path BFS between two types | no |
| `topsolid_get_state` | Active document, project, connection status | yes |
| `topsolid_execute_script` | Run custom C# (read-only) | yes |
| `topsolid_modify_script` | Run custom C# (write, Pattern D auto-wrapped) | yes |
| `topsolid_get_recipe` | Return the C# source of a recipe | no |
| `topsolid_compile` | Roslyn dry-run compile against the TopSolid SDK | no |
| `topsolid_search_examples` | Search user-local private corpora of C# samples | no |
| `topsolid_whats_new` | API changelog between two TopSolid versions | no |
| `topsolid_search_help` | FTS5 search over 5809 help pages (EN+FR) | no |
| `topsolid_search_commands` | Lookup UI commands in the 2428-command catalog | no |

## Why this exists

TopSolid ships a powerful .NET Automation API, but it is hard to discover: 1728 methods across 46 interfaces, ~10-20% of the product surface is only reachable via ribbon/menu commands, and half the "how do I..." answers are in help pages no one reads. This server collapses that discovery cost for an AI agent — and for a human developer who treats the MCP as a knowledge base for writing standalone TopSolid Automation apps.

## Repository layout

```
topsolid-automation-mcp/
├── server/        TopSolid MCP Server (.NET 4.8, stdio JSON-RPC)
├── graph/         API graph builder (.NET 4.8, reflection-based)
├── bridge/        HTTP/SSE bridge for remote clients (Node, mcp-proxy wrapper)
├── scripts/       Graph enrichment, help index, commands catalog, privacy scan (Python)
├── data/          graph.json, api-index.json, help.db, commands-catalog.json
├── docs/          VitePress documentation site
├── tests/         Automated test harness (PowerShell)
└── skills/        Optional YAML skills for MCP-aware agents
```

## Contributing

Read [CONTRIBUTING.md](./CONTRIBUTING.md). Short version: pick up a [good-first-issue](https://github.com/Julien38300/topsolid-automation-mcp/labels/good%20first%20issue), add a recipe to `server/src/Tools/RecipeTool.cs`, or improve the docs. Every PR helps.

All interactions are governed by our [Code of Conduct](./CODE_OF_CONDUCT.md).

## License

[MIT](./LICENSE). Use commercially, fork, modify freely. TopSolid itself is proprietary — you need your own license from TOPSOLID SAS to run this against a live TopSolid instance.
