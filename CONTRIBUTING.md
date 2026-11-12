# Contributing to TopSolid MCP

Thanks for wanting to help. This project lets AI agents drive TopSolid 7 through its public Automation API. It grows from what TopSolid users find out in their own work: a recipe that saves them an hour, an API pitfall, a fix, a test on another TopSolid version.

You do not need to write C# to contribute. There are five ways in:

| You have... | Do this | Needs TopSolid |
|---|---|---|
| a bug or a crash | [Report a bug](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=bug_report.md) (or use *Report a bug* in the tray menu: version, OS and connection state are filled in for you) | no |
| a task you would like a recipe for | [Request a recipe](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_request.yml) | no |
| a script that worked in `topsolid_execute_script` | [Propose it as a recipe](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_proposal.yml), or open a pull request (below) | yes, to test it |
| found an API trap (wrong unit, forbidden call, dialog that blocks TopSolid...) | [Share an API pitfall](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=api_pitfall.yml): it goes into the agent skills | no |
| run the server on a TopSolid version or setup we do not list | [Report compatibility](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=compatibility_report.yml) | yes |

Questions and show-and-tell go to [Discussions](https://github.com/Julien38300/topsolid-automation-mcp/discussions). Good first tasks carry the [`good first issue`](https://github.com/Julien38300/topsolid-automation-mcp/labels/good%20first%20issue) label.

## Ground rules

1. **Only public material.** Use the public TopSolid help (help.topsolid.com), the Automation SDK installed with TopSolid, and your own tests. Never submit documentation that is not public, code or data from your employer, customer files, part names or project paths from real jobs. When in doubt, leave it out and ask in the issue.
2. **Sign off your commits** ([Developer Certificate of Origin](https://developercertificate.org/)): `git commit -s` adds `Signed-off-by: Your Name <you@example.com>`. It certifies that you wrote the change, or have the right to submit it, under the project's MIT licence. A check fails on pull requests whose commits are not signed off.
3. **No secrets, no personal paths.** No API key, user name, machine name, `C:\Users\...` path or internal host in code, docs, tests or screenshots. CI runs `scripts/privacy-scan.py` on every pull request.
4. **English in code**: recipe names, descriptions, C# identifiers and comments. The documentation site is in French; English pages are welcome.
5. Follow the [Code of Conduct](./CODE_OF_CONDUCT.md). Security problems go through [SECURITY.md](./SECURITY.md), not public issues.

## Set up

- Windows, with **TopSolid 7.15 or later** and Automation enabled (*Tools > Options > General > Automation*, *Manage remote access*, port 8090) to build and run the server.
- [.NET SDK 8](https://dotnet.microsoft.com/download) (it builds the .NET Framework 4.8 projects) and Python 3.11.

```powershell
git clone https://github.com/<you>/topsolid-automation-mcp
cd topsolid-automation-mcp
dotnet build server/src/TopSolidMcpServer.csproj
```

The build finds the TopSolid Automation assemblies in the TopSolid installation (`C:\Program Files\TOPSOLID\TopSolid 7.*\bin`). Elsewhere, set `TOPSOLID_BIN_PATH` or pass `/p:TopSolidBinPath=...`. They are not redistributable, which is why CI cannot build the server itself.

**Without TopSolid** you can still run the unit tests, the recipe and privacy checks, and work on the docs:

```powershell
python scripts/ci_gate.py          # what CI runs: Python checks, recipes, privacy, 3 unit-test suites, graph build
cd docs; npm ci; npm run dev       # documentation site on http://localhost:5173
```

## Add a recipe

A recipe is a C# snippet that `topsolid_run_recipe` runs by name, so that an agent (even a small local model) does not have to write code.

1. **Write and test it live first** with `topsolid_compile`, then `topsolid_execute_script`, on your own TopSolid, until it does what you want on a real document.
2. **Add it to `server/src/Tools/RecipeTool.cs`**, next to recipes of the same category, with the factory that matches what it does:

   | Factory | Mode | Use when the recipe... |
   |---|---|---|
   | `R(...)` | read | only reads |
   | `RW(...)` | write | changes the model: it runs inside `StartModification` / `EndModification` and the document is saved |
   | `RN(...)` | write, no transaction | only makes PDM or document calls (`Pdm.SetDescription`, `Pdm.Save`, `Documents.Open`...), which TopSolid refuses inside a transaction |
   | `RD(...)` | disk | writes files (exports) |

   ```csharp
   { "read_part_number", R(
       "Returns the part number (reference) of the edited document",
       @"
       DocumentId docId = TopSolidHost.Documents.EditedDocument;
       if (docId.IsEmpty) return ""No document open."";
       PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
       return TopSolidHost.Pdm.GetPartNumber(pdmId);
       "
   ) },
   ```

   Rules: C# 5 (no `$"..."`, no `?.`), SI units (metres, radians, kilograms), always return a string, a clear message when no document is open. A recipe that takes an argument uses `{value}` in its body. `topsolid_api_help` gives the exact signature and, for each method, whether it must run inside or outside a transaction.
3. **Add its name to `server/data/recipe-list.txt`**, on its own line, at the same place as in `RecipeTool.cs`.
4. **Check**: `python scripts/check-recipes.py`. It fails when a read recipe writes, when a name is duplicated, or when `recipe-list.txt` misses a recipe.
5. **Regenerate the readable catalogue** from the built server, never by hand:

   ```powershell
   dotnet build server/src/TopSolidMcpServer.csproj
   python server/scripts/gen-recipes-catalog.py server/src/bin/Debug/net48/TopSolidMcpServer.exe
   ```
6. In the pull request, say **which TopSolid version you ran it on, on what kind of document, and what it returned**. A recipe that has never run on a real TopSolid is not merged.

## Improve the agent skills

`skills/` holds the instructions AI agents read before driving TopSolid: which recipe to use, the pitfalls of the API, the trade rules (sheet metal, railings...). The files in this repository are the reference.

- Edit the Markdown in `skills/<skill>/` and open a pull request, or use the [API pitfall](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=api_pitfall.yml) form if you prefer to describe it.
- Every rule needs its evidence: the TopSolid version, what you ran, what happened.
- Only cite recipes that exist (`topsolid_list_recipes`), and never a count of recipes or methods: counts go stale.

## Documentation

The site (VitePress, in `docs/`) is published from `main` to [GitHub Pages](https://julien38300.github.io/topsolid-automation-mcp/). Fix a page, add an example from your own use, translate a page into English: all welcome. Do not copy pages of the TopSolid help; link to them.

## Tests

- **Unit tests** need no TopSolid and run in CI: `dotnet test` on `server/tests/TopSolidMcpServer.Tests`, `TopSolidMcp.Http.Tests` and `TopSolidMcp.UpdateCheck.Tests`, plus `python -m pytest tests/python`. New logic comes with its tests.
- **Live tests** (`tests/run-tests.ps1`, `tests/test_recipes_live.ps1`) run against a TopSolid session and a reference test document that is not published yet. Until it is, describe your manual test in the pull request.

## Pull requests

1. Fork, create a branch, keep the change focused on one thing.
2. Commit with [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `test:`, `chore:`) and sign off (`git commit -s`).
3. Run `python scripts/ci_gate.py` (or at least `python scripts/check-recipes.py` and `python scripts/privacy-scan.py`) before pushing.
4. Open the pull request and fill in its template. On a first contribution, GitHub waits for the maintainer to approve the CI run: this is normal.
5. The project has one maintainer: expect a first answer within a few days. Releases are built by the maintainer on a machine with TopSolid, and the release notes credit every contributor.

By contributing, you agree that your contribution is licensed under the [MIT licence](./LICENSE) of the project.
