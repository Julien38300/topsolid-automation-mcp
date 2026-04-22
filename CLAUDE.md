# topsolid-automation-mcp — Project Rules

**Community-maintained** Model Context Protocol server for TopSolid® 7 CAD/PDM Automation API. Independent project, not affiliated with TOPSOLID SAS.

## Repository structure

```
topsolid-automation-mcp/
├── server/        — TopSolid MCP Server (.NET 4.8, stdio JSON-RPC)
├── graph/         — TopSolid API Graph builder (.NET 4.8)
├── bridge/        — HTTP/SSE bridge for remote MCP clients (Node wrapper)
├── scripts/       — Python utilities (graph enrichment, help index, catalog builder)
├── data/          — graph.json, api-index.json, help.db, commands-catalog.json
├── docs/          — VitePress documentation site
├── tests/         — Automated test harness
├── research/      — Roadmap, missions, decisions (internal planning)
└── skills/        — Optional YAML skills for MCP-aware agents
```

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a PR. Short version:

- One file per class (.NET).
- New recipes go into `server/src/Tools/RecipeTool.cs`. Keep them self-contained and under ~60 lines of C#.
- Never commit secrets. `.gitignore` covers the usual suspects.
- CI runs `scripts/privacy-scan.py` on every push — see that file for the regex-guarded patterns.

## Language conventions

- **Code** (identifiers, inline comments): English.
- **MCP tool descriptions** (visible to any MCP client via `tools/list`): English.
- **User-facing doc site prose**: French (the primary audience is French-speaking CAD users). English PRs welcome.
- **XML doc comments**: English.

## Tech stack — non-negotiable

- **.NET Framework 4.8**, C# 7.3 (TopSolid Automation assemblies target this runtime).
- **Newtonsoft.Json 13** for JSON-RPC serialization.
- **Microsoft.Data.Sqlite 6.0.x** for the embedded help FTS5 index.
- **VitePress 1.6** for the docs site.
- **Python 3.11+** for the enrichment / catalog scripts.
- MCP protocol version **2025-03-26** (Streamable HTTP) + legacy 2024-11-05 fallback.

## Runtime contracts

- Logs on `stderr`. `stdout` is reserved for the MCP JSON-RPC protocol — a single stray `Console.WriteLine` breaks the client.
- `graph.json` is loaded once at startup, held in memory.
- The server must never crash — catch exceptions at the top level and return a JSON-RPC error.
- Mutex `Global\\TopSolidMcpServer_Singleton` enforces single-instance locally.

## TopSolid glossary (FR → EN mapping)

| FR | EN API |
|---|---|
| Désignation | Description |
| Référence | PartNumber |
| Mise au coffre | CheckIn |
| Sorti de coffre | CheckOut |
| Mise à plat | Unfolding |
| Rafale | batch generation from BOM |
| Nomenclature | Bill of Materials (BOM) |
| Mise en plan | Drafting |

## Independence

This project is community-maintained and independent. It is not endorsed by, sponsored by, or affiliated with TOPSOLID SAS. "TopSolid" is a registered trademark of TOPSOLID SAS.
