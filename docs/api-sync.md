# TopSolid API Sync Pipeline

Automated pipeline to keep the TopSolid API graph and recipe catalog in sync
with each new TopSolid release. Reads the `TopSolid'Design Automation.chm`
from the installed TopSolid version, extracts method documentation, diffs
against the previous snapshot, and enriches `graph.json`.

## Quick start

```bash
# Install dependencies (one-time)
make sync-deps

# Run the full pipeline (auto-detects newest TopSolid install)
make sync-api
```

Outputs are written under `data/api/<version>/` and `data/`:

- `data/api/<version>/raw/*.htm` — extracted CHM pages (gitignored)
- `data/api/<version>/methods.json` — structured method list
- `data/api/<version>/types.json` — interfaces, classes, enums
- `data/api/<version>/namespaces.json` — namespace → types index
- `data/api/<version>/meta.json` — CHM hash, extraction timestamp
- `data/api/<version>/proposals.json` — machine-readable recipe proposals
- `data/api-diff-<version>.json` — delta vs previous snapshot
- `data/recipe-proposals-<version>.md` — human-readable proposals
- `data/changelog-<version>.md` — summary changelog
- `data/graph.json` — enriched with CHM data (backup: `graph.json.pre-<version>.bak`)

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
2. Run `make sync-api` — the pipeline auto-detects the newest install.
3. Review `data/changelog-<version>.md` for breaking changes and deprecations.
4. Review `data/recipe-proposals-<version>.md` — copy approved recipes into `RecipeTool.cs`.
5. Rebuild the MCP server and re-run the LoRA training if recipes were added.

## Idempotency & safety

- Re-running with the same CHM hash skips extraction.
- `graph.json` is backed up before every enrich (`graph.json.pre-<version>.bak`).
- `data/api/<version>/` snapshots are immutable — never overwritten.
- `--dry-run` prints what would happen without writing files.

## Files

- Orchestrator: `scripts/sync-topsolid-api.py`
- Library: `scripts/lib/` (paths, chm_extractor, html_parser, api_model, differ, graph_merger, recipe_proposer, reporter)
- Dependencies: `scripts/requirements-sync.txt` (beautifulsoup4, lxml, jsonschema)
