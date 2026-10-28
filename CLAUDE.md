# topsolid-automation-mcp — AI Agent Onboarding & Project Rules

**Community-maintained** Model Context Protocol (MCP) server for TopSolid® 7 CAD/PDM Automation API (`TopSolid 7.15` to `7.21`). Independent project, not affiliated with TOPSOLID SAS.

---

## 0. AI Agent Quick Router — Read This First!

If a user gave you this repository and said *"set this up"* or *"use this to control TopSolid"*, follow the matching path below immediately:

### Path A — The user wants to **install or connect** TopSolid MCP
1. **Download or build**:
   - Fastest (no build tools required): download the latest `TopSolidMcpServer-vX.Y.Z.zip` from [GitHub Releases](https://github.com/Julien38300/topsolid-automation-mcp/releases/latest), unzip it, and run `install\Installer_TopSolidMCP.bat`.
   - Or build from source on Windows (.NET Framework 4.8): `powershell -ExecutionPolicy Bypass -File server/scripts/build-release.ps1`.
2. **Connect your MCP client**:
   - **Native Streamable HTTP (recommended, default port `8080`)**: point your client to `http://127.0.0.1:8080/mcp` with header `X-API-Key: <key>` (can be copied/toggled from the Windows system tray icon -> *Paramètres du serveur MCP*).
   - **stdio**: launch `TopSolidMcpServer.exe` directly (e.g. `claude mcp add --scope user topsolid C:\TopSolidMCP\TopSolidMcpServer.exe`).
3. **Install the bundled AI skills** (optional but strongly recommended):
   Copy the 4 folders inside [`skills/`](./skills/) (`topsolid-mcp`, `topsolid-mcp-modeling`, `topsolid-api-knowledge`, `topsolid-railing-design`) into your agent's skill directory (`~/.claude/skills/`, `~/.codex/skills/`, `~/.gemini/config/skills/`, or your IDE rules folder).

### Path B — The user wants you to **pilot TopSolid (CAD / PDM / Drafting / BOM)**
**STOP and read the bundled skills in [`skills/`](./skills/) before calling tools:**
1. **[`skills/topsolid-mcp/SKILL.md`](./skills/topsolid-mcp/SKILL.md)** — **Mandatory for all TopSolid tasks**: 208+ pre-built recipes (`topsolid_run_recipe`), Read (`R`) vs Write (`RW`/`RD`) confirmation rules, PDM properties, parameters, STEP/DXF/PDF exports, and UI commands (`invoke_command`).
2. **[`skills/topsolid-mcp-modeling/SKILL.md`](./skills/topsolid-mcp-modeling/SKILL.md)** — **Mandatory for 3D modeling & assemblies**: 100% Recipe-First workflow (`create_project_tree`, `create_extruded_bar`, `create_sheet_metal_plate`, `create_profile_by_two_points`, `batch_add_inclusions`, `update_where_used_and_save`, `check_collisions`).
3. **[`skills/topsolid-api-knowledge/SKILL.md`](./skills/topsolid-api-knowledge/SKILL.md)** — **Mandatory before writing any C# script**: C# 5 compiler constraints (`CSharpCodeProvider`, no `$"..."` string interpolation), SI units (meters/radians/kg), transaction rules (`StartModification` / `EndModification`), and 3D sketch orientation.
4. **[`skills/topsolid-railing-design/SKILL.md`](./skills/topsolid-railing-design/SKILL.md)** — Specialized trade rules for guardrails / railings (NF P01-012 / NF P01-013, TopSolid'Steel practices).

### Path C — You are a **small or local model** (e.g. Gemma 4, Ministral 3B/8B, Llama 8B, Qwen 7B via Ollama / LM Studio / OpenWebUI)
- **NEVER generate C# code**: do **NOT** call `topsolid_execute_script` or `topsolid_modify_script`. Small models hallucinate TopSolid C# namespaces and fail C# 5 compilation.
- **100% Recipe-First**: use **ONLY**:
  1. `topsolid_list_recipes(category, search)` to find the exact recipe name,
  2. `topsolid_run_recipe(recipe, value)` to execute it,
  3. `topsolid_search_commands(query)` + `topsolid_run_recipe(recipe="invoke_command", value="...")` for ribbon/menu commands (`invoke_command` resolves command names case-insensitively and accepts either the full namespace path or the short command class name).

---

## Repository structure

```
topsolid-automation-mcp/
├── server/        — TopSolid MCP Server (.NET 4.8, stdio + native HTTP on port 8080)
│   ├── src/       — Program.cs, Protocol/, Tools/, Utils/ (System Tray UI)
│   ├── data/      — runtime data shipped next to the .exe:
│   │                graph.json, help.db, commands-catalog.json, recipe-list.txt
│   ├── scripts/   — build-release.ps1, publish-release.py, update.ps1, sync-topsolid-api.py
│   └── models/    — Ollama Modelfile for the local code sub-agent
├── skills/        — 4 official Agent Skills (topsolid-mcp, topsolid-mcp-modeling,
│                    topsolid-api-knowledge, topsolid-railing-design)
├── graph/         — TopSolid API Graph builder (.NET 4.8)
├── bridge/        — Legacy HTTP/SSE bridge (v1.7, superseded by native HTTP in v1.8+)
├── scripts/       — Python utilities (graph enrichment, help index, catalog builder, LoRA)
├── data/          — build-time artefacts: graph.json, api-index.json,
│                    recipe-name-mapping-fr-en.json, recipes.md
├── docs/          — VitePress documentation site
└── tests/         — Automated test harness (C# runner + PowerShell scripts)
```

`help.db` and the other files the server reads at runtime live in **`server/data/`**, not in the
top-level `data/`. The top-level `data/` holds the artefacts produced by the Python pipelines.

## Recipe pipeline — mandatory after any recipe change

The recipe catalogue is the single source of truth in `server/src/Tools/RecipeTool.cs`
(**208 recipes** today). Several generated files repeat it — `server/data/recipe-list.txt`,
`server/data/recipes.md`, `data/recipes.md`, the docs pages, the skill package and the LoRA
dataset — so they drift the moment a recipe is added by hand.

After adding or modifying a recipe:

```bash
make sync-ecosystem   # prints the files you must still update BY HAND — it syncs nothing
make check            # check-privacy + check-recipes; must exit 0 before you commit
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
- **User-facing doc site prose & tray UI**: French (the primary audience is French-speaking CAD users). English PRs welcome.
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

## MCP protocol & transports

- **Dual transport in a single process (v1.8.0+)**:
  1. **stdio** (newline-delimited JSON-RPC over stdin/stdout).
  2. **Native Streamable HTTP** (`http://127.0.0.1:8080/mcp` by default), protected by `X-API-Key` (DPAPI-encrypted in `settings.json`, configurable from the Windows system tray icon) and `Origin` header validation.
- Versions accepted at `initialize` (see `McpRouter`): **2025-06-18**, **2025-03-26**, **2024-11-05**.
- `initialize` also returns built-in MCP `instructions` so clients without local skills still follow the Recipe-First workflow.

## Runtime contracts

- Logs on `stderr`. `stdout` is reserved for the MCP JSON-RPC protocol — a single stray `Console.WriteLine` breaks stdio clients.
- `graph.json` is lazy-loaded on the **first tool call that needs it**, then held in memory for the process lifetime.
- The TopSolid connection is opened immediately at startup (with background auto-reconnect backoff) so the tray icon reflects the real state.
- Design and Drafting Automation modules are **optional**: they are probed at connect time (`TopSolidConnector.HasDesignModule` / `HasDraftingModule`).
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
| `--http-standalone` | — | Run the native HTTP server + system tray without waiting on stdin |
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
