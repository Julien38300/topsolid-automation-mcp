# TopSolid API Sync Pipeline

Automated pipeline to keep the TopSolid API graph and recipe catalog in sync
with each new TopSolid release. Reads the `TopSolid'Design Automation.chm`
from the installed TopSolid version, extracts method documentation, diffs
against the previous snapshot, and enriches `graph.json`.

## Quick start

The `sync-*` targets live in `server/Makefile`, so every command below runs from `server/`:

```bash
cd server

# Install dependencies (one-time)
make sync-deps

# Run the full pipeline (auto-detects newest TopSolid install)
make sync-api
```

Outputs are written under `server/data/`:

- `server/data/api/<version>/raw/*.htm` — extracted CHM pages (gitignored)
- `server/data/api/<version>/methods.json` — structured method list
- `server/data/api/<version>/types.json` — interfaces, classes, enums
- `server/data/api/<version>/namespaces.json` — namespace → types index
- `server/data/api/<version>/meta.json` — CHM hash, extraction timestamp
- `server/data/api/<version>/proposals.json` — machine-readable recipe proposals
- `server/data/api-diff-<version>.json` — delta vs previous snapshot
- `server/data/recipe-proposals-<version>.md` — human-readable proposals
- `server/data/changelog-<version>.md` — summary changelog
- `server/data/graph.json` — enriched with CHM data (backup: `graph.json.pre-<version>.bak`)

The generated changelog stays local: it is **not** shipped in the release and no MCP tool serves
it (the former `topsolid_whats_new` tool was removed for exactly that reason). Read
`changelog-<version>.md` yourself after a sync.

## Pipeline stages

| Stage | What it does |
|---|---|
| `extract` | CHM → `raw/*.htm` (via 7-Zip, ~3s for 3.5 MB / 2970 pages) |
| `parse` | `raw/*.htm` → `methods.json`, `types.json`, `namespaces.json` via BeautifulSoup4 |
| `diff` | Compare vs previous snapshot → `api-diff-<version>.json` |
| `enrich` | Merge CHM descriptions/remarks/since/deprecated into `graph.json` edges |
| `propose` | Suggest new recipes for CHM-only methods (Green/Yellow/Red triage) |
| `report` | Generate `changelog-<version>.md` |

## Recipe proposal triage

Each CHM-only method (not present in graph) is classified:

- **🟢 Green** — simple read (`Get*`, `Read*`, `List*`) with scalar return type + no remarks. Code template ready for review.
- **🟡 Yellow** — transactional (remarks mention `StartModification`, or name is `Set*/Create*/Remove*/Delete*`). Pattern D skeleton emitted.
- **🔴 Red** — unclear (out/ref params, generics, operator overloads, deprecated). Stub only, manual implementation needed.

Proposals are **never auto-committed** to `RecipeTool.cs`. Human review required.

## Adding a new TopSolid version

1. Install the new TopSolid version (e.g. `C:\Program Files\TOPSOLID\TopSolid 7.22\`).
2. Run `cd server && make sync-api` — the pipeline auto-detects the newest install.
3. Review `server/data/changelog-<version>.md` for breaking changes and deprecations.
4. Review `server/data/recipe-proposals-<version>.md` — copy approved recipes into `RecipeTool.cs`.
5. Run `make sync-ecosystem` — it prints the derived files you must update by hand — then `make check`, which re-checks the catalogue in `RecipeTool.cs` itself (count, `R()`/`RW()`/`RD()` classification, unique names, no C# 6 interpolation). It does not compare the derived files, so verify those by reading them.
6. Rebuild the MCP server and re-run the LoRA training if recipes were added.

> **Install layout.** CHM auto-detection only scans `C:\Program Files\TOPSOLID\TopSolid *`.
> A historical Missler layout (`C:\Missler\V627\...`) is not found by this pipeline, even though
> the MCP server itself resolves it at runtime. Point the extractor at the CHM by hand in that case.

## Idempotency & safety

- Re-running with the same CHM hash skips extraction.
- `graph.json` is backed up before every enrich (`graph.json.pre-<version>.bak`).
- `server/data/api/<version>/` snapshots are immutable — never overwritten.
- `--dry-run` prints what would happen without writing files.

## Files

- Orchestrator: `server/scripts/sync-topsolid-api.py`
- Library: `server/scripts/lib/` (paths, chm_extractor, html_parser, api_model, differ, graph_merger, recipe_proposer, reporter)
- Dependencies: `server/scripts/requirements-sync.txt` (beautifulsoup4, lxml, jsonschema)
