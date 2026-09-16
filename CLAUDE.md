# topsolid-automation-mcp — Project Rules

**Community-maintained** Model Context Protocol server for TopSolid® 7 CAD/PDM Automation API. Independent project, not affiliated with TOPSOLID SAS.

## Repository structure

```
topsolid-automation-mcp/
├── server/        — TopSolid MCP Server (.NET 4.8, stdio JSON-RPC)
│   ├── src/       — Program.cs, Protocol/, Tools/, Utils/
│   ├── data/      — runtime data shipped next to the .exe:
│   │                graph.json, help.db, commands-catalog.json, recipe-list.txt
│   ├── scripts/   — build-release.ps1, update.ps1, sync-topsolid-api.py
│   └── models/    — Ollama Modelfile for the local code sub-agent
├── graph/         — TopSolid API Graph builder (.NET 4.8)
├── bridge/        — HTTP/SSE bridge for remote MCP clients (Node wrapper)
├── scripts/       — Python utilities (graph enrichment, help index, catalog builder)
├── data/          — build-time artefacts: graph.json, api-index.json,
│                    recipe-name-mapping-fr-en.json, recipes.md
├── docs/          — VitePress documentation site
├── tests/         — Automated test harness (C# runner + PowerShell scripts)
└── skills/        — Optional skill package for MCP-aware agents
```

`help.db` and the other files the server reads at runtime live in **`server/data/`**, not in the
top-level `data/`. The top-level `data/` holds the artefacts produced by the Python pipelines.

## Recipe pipeline — mandatory after any recipe change

The recipe catalogue is the single source of truth in `server/src/Tools/RecipeTool.cs`
(**132 recipes** today). Several generated files repeat it — `server/data/recipe-list.txt`,
`server/data/recipes.md`, `data/recipes.md`, the docs pages, the skill package and the LoRA
dataset — so they drift the moment a recipe is added by hand.

After adding or modifying a recipe:

```bash
make sync-ecosystem   # prints the files you must still update BY HAND — it syncs nothing
make check            # check-privacy + check-recipes; must exit 0 before you commit
```

`sync-ecosystem` is **not implemented**: no script has ever stood behind that name. It is a
checklist that exits 0. The synchronisation itself is manual — `data/recipes.md`,
`server/data/recipes.md`, `server/data/recipe-list.txt`, `skills/`, `docs/`, and
`make lora-dataset` for the training set. `make check` verifies `RecipeTool.cs` itself — it re-counts the catalogue and checks that every
entry uses an `R()` / `RW()` / `RD()` factory, that names are unique, that no `R()` recipe calls a
write API, and that no body uses C# 6 interpolation. It does **not** read the derived files: their
counts drift silently (`server/data/recipe-list.txt` still announces 115), so re-generate them by
hand and re-read them yourself.

Both targets exist in the root `Makefile` and in `server/Makefile`; run them from either.

After a new TopSolid version:

```bash
cd server
make sync-api         # extract CHM → diff → enrich graph → propose new recipes
```

Never commit `RecipeTool.cs` without `make check` passing (exit 0).

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
- Dynamic script compilation uses **`CSharpCodeProvider`** (the `csc` compiler bundled with the
  .NET Framework), not Roslyn. Generated snippets are therefore compiled as **C# 5**: no string
  interpolation, no `?.`, no `nameof`. `LangVersion 7.3` applies to the server's own sources only.

## MCP protocol

- Transport: **stdio** (JSON-RPC over stdin/stdout). The HTTP bridge in `bridge/` is optional and
  is a `mcp-proxy` wrapper around that same stdio process — the server itself never listens on HTTP.
- Versions accepted at `initialize` (see `McpRouter`): **2025-06-18**, **2025-03-26**, **2024-11-05**.
  The server echoes the client's version when it is one of those, and otherwise falls back to the
  newest supported one.

## Runtime contracts

- Logs on `stderr`. `stdout` is reserved for the MCP JSON-RPC protocol — a single stray `Console.WriteLine` breaks the client.
- `graph.json` is lazy-loaded on the **first tool call that needs it**, then held in memory for the
  process lifetime. Startup itself stays cheap.
- The TopSolid connection is opened immediately at startup so the tray icon reflects the real state.
- Design and Drafting Automation modules are **optional**: they are probed at connect time
  (`TopSolidConnector.HasDesignModule` / `HasDraftingModule`), reported on `stderr` and in
  `topsolid_get_state`, and their namespace is imported into generated scripts only when the
  assembly is present. A missing module must never take the server down (issue #11): a recipe that
  needs it fails with a compilation error, the process keeps serving everything else. Nothing
  currently un-registers the tools themselves — if you wire that up, read those two properties.
- The server must never crash — catch exceptions at the top level and return a JSON-RPC error.
- Mutex `Global\\TopSolidMcpServer_Singleton` enforces single-instance locally.

## Script execution — trust model

`topsolid_execute_script` and `topsolid_modify_script` compile user-supplied C# and run it
**in-process, fully trusted**. "Read-only" means *outside a TopSolid modification transaction*; it
is not a sandbox, and `System.IO` stays available because export recipes need it. The deny-list in
`ScriptExecutor.BlockedSymbols` is a guardrail against a model wandering off, not a security
boundary. Documentation must never describe these tools as safe or sandboxed, and must tell users
not to auto-approve them in their MCP client.

## Flags and environment variables

| Flag | Environment variable | Effect |
|---|---|---|
| `--read-only` | `TOPSOLID_MCP_READ_ONLY=1` | `topsolid_modify_script` is not registered; recipes run in read-only mode |
| `--no-tray` | `TOPSOLID_MCP_NO_TRAY=1` | No system-tray icon (headless / session 0) |
| `--port <n>` | — | TopSolid Automation port (default 8090) |
| `--compile <file>` | — | Dry-run compile a file and exit (0 = OK) |
| `--version` / `-v` | — | Print the version and exit |
| — | `TOPSOLID_BIN_PATH` | Folder holding `TopSolid.Kernel.Automating.dll`, when auto-detection fails |
| — | `TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC` | Script execution timeout (default 60 s) |
| — | `TOPSOLID_MCP_ALLOW_UNSAFE=1` | Disables the blocked-API check — debugging only |

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

## Independence & data sources

This project is community-maintained and independent. It is not endorsed by, sponsored by, or affiliated with TOPSOLID SAS. "TopSolid®" is a registered trademark of TOPSOLID SAS.

All knowledge the server ships comes from publicly available TopSolid material:
- the Automation `.dll` assemblies included in every TopSolid install (reflected into `graph.json` + `api-index.json`);
- the public help site at https://help.topsolid.com/ (indexed into `help.db` and `commands-catalog.json`).

No proprietary sample code and no identified individual's private code is bundled. Private corpus access (`topsolid_search_examples`) is opt-in via environment variables — paths live only on the user's own machine.
