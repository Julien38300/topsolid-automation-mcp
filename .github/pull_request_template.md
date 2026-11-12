## What and why

<!-- What this changes, and the problem or need behind it. Link the issue: "Closes #123". -->

## How it was tested

<!-- For a recipe or a change that talks to TopSolid: TopSolid version, kind of document, what it returned.
     For logic that needs no TopSolid: the unit tests you added. -->

- TopSolid version:
- Tested on:
- Result:

## Checklist

- [ ] Commits are signed off (`git commit -s`, see [CONTRIBUTING.md](https://github.com/Julien38300/topsolid-automation-mcp/blob/main/CONTRIBUTING.md#ground-rules)).
- [ ] No employer or customer data, no non-public TopSolid documentation, no secret or personal path.
- [ ] `python scripts/check-recipes.py` and `python scripts/privacy-scan.py` pass (or `python scripts/ci_gate.py`).
- [ ] New logic comes with unit tests; a new recipe ran on a real TopSolid.
- [ ] A new recipe is also in `server/data/recipe-list.txt`, and `server/data/recipes.md` was regenerated.
- [ ] Docs updated if the behaviour changes (`README.md`, `docs/guide/`).
