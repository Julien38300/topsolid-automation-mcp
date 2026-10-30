# AGENTS.md — Universal AI Agent Guide for `topsolid-automation-mcp`

This file is the entry point for all AI coding & CAD agents (**Claude Code, OpenAI Codex, Cursor, Windsurf, Cline, Continue, Google Antigravity, OpenCode, Goose, and local models via Ollama / LM Studio / OpenWebUI**).

---

## 1. What are you here to do? (Pick your role)

### Role 1 — Install & Connect TopSolid MCP for the User
If the user gave you this repository and asked you to install or configure it:
1. **Enable TopSolid Automation** in TopSolid: `Tools > Options > General > Automation` -> check **Manage remote access** (port `8090`) and restart TopSolid.
2. **Install the Server**:
   - **Pre-built Release (recommended)**: download the latest `TopSolidMcpServer-vX.Y.Z.zip` from [GitHub Releases](https://github.com/Julien38300/topsolid-automation-mcp/releases/latest), unzip it, and run `install\Installer_TopSolidMCP.bat`.
   - **From Source**: run `powershell -ExecutionPolicy Bypass -File server/scripts/build-release.ps1`.
3. **Connect the MCP Client**:
   - **HTTP (recommended)**: `http://127.0.0.1:8080/mcp` (manage or disable the `X-API-Key` from the Windows system tray icon -> *Paramètres du serveur MCP*).
   - **stdio**: point your MCP client to `TopSolidMcpServer.exe`.
4. **Install the Skills**: copy the 4 folders under [`skills/`](./skills/) into your agent's skill directory (`~/.claude/skills/`, `~/.codex/skills/`, `~/.gemini/config/skills/`, etc.).

---

### Role 2 — Pilot TopSolid (CAD / PDM / Assemblies / Drafting)
**Before calling any MCP tool, read the relevant skill in [`skills/`](./skills/):**

| Task | Skill to Read First |
|---|---|
| Reading/writing PDM properties, parameters, BOM, STEP/DXF/PDF exports, invoking UI commands | [`skills/topsolid-mcp/SKILL.md`](./skills/topsolid-mcp/SKILL.md) |
| Creating 3D parts, extruded bars (`Extruded Bar`), sheet metal / drilled plates, assemblies, B-Rep collision checks | [`skills/topsolid-mcp-modeling/SKILL.md`](./skills/topsolid-mcp-modeling/SKILL.md) |
| Writing custom C# 5 scripts (`topsolid_compile` / `topsolid_execute_script` / `topsolid_modify_script`) | [`skills/topsolid-api-knowledge/SKILL.md`](./skills/topsolid-api-knowledge/SKILL.md) |
| Designing guardrails / railings (garde-corps NF P01-012 / NF P01-013) | [`skills/topsolid-railing-design/SKILL.md`](./skills/topsolid-railing-design/SKILL.md) |

---

### Role 3 — Small / Local LLM Rules (Gemma 4, Ministral, Llama, Qwen 3B–12B)
If you are running on a local or compact model:
1. **NEVER call `topsolid_execute_script` or `topsolid_modify_script`** (do not write raw C# code).
2. **Use 100% `topsolid_run_recipe`**:
   - Call `topsolid_list_recipes(category, search)` to find the exact recipe name.
   - Call `topsolid_run_recipe(recipe="<name>", value="<params>")` to execute it.
3. **UI / Ribbon Commands**:
   - Call `topsolid_search_commands(query="<keyword>")` to find a command.
   - Execute it via `topsolid_run_recipe(recipe="invoke_command", value="<fullName_or_commandName>")`. Note: `invoke_command` automatically resolves command names case-insensitively and accepts either the full namespace (`TopSolid.Cad.Wood.UI.ConstructionVolumes.Sketches.CreateLayoutSketchCommand`) or the short class name (`CreateLayoutSketchCommand` / `createlayoutsketchcommand`).

---

### Role 4 — Contribute to or Maintain the Repository
See [`CLAUDE.md`](./CLAUDE.md) and [`CONTRIBUTING.md`](./CONTRIBUTING.md) for the C# (.NET Framework 4.8) build, test suites, `make check` quality gate, and release workflow.
