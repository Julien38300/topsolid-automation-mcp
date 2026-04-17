"""Markdown changelog generator for a TopSolid API sync run."""
from __future__ import annotations

import json
from pathlib import Path


def generate_changelog(
    version: str,
    diff_path: Path,
    proposals_json_path: Path | None,
    out_path: Path,
) -> None:
    diff = json.loads(diff_path.read_text(encoding="utf-8"))
    proposals: list[dict] = []
    if proposals_json_path and proposals_json_path.exists():
        proposals = json.loads(proposals_json_path.read_text(encoding="utf-8"))

    s = diff["summary"]

    lines = []
    lines.append(f"# TopSolid API Sync — {version}\n")
    lines.append(f"**From version:** `{diff['from_version']}`  ")
    lines.append(f"**To version:** `{diff['to_version']}`  ")
    lines.append(f"**Mode:** `{diff['mode']}`  ")
    lines.append(f"**Method count:** {diff['prev_count']} → {diff['curr_count']}  \n")

    lines.append("## Summary\n")
    lines.append("| Change | Count |")
    lines.append("|---|---|")
    lines.append(f"| Added               | {s['added']} |")
    lines.append(f"| Removed             | {s['removed']} |")
    lines.append(f"| Signature changed   | {s['changed_signature']} |")
    lines.append(f"| Description changed | {s['changed_description']} |")
    lines.append(f"| Newly deprecated    | {s['deprecated']} |")
    lines.append(f"| Undeprecated        | {s['undeprecated']} |")
    lines.append("")

    if proposals:
        green = sum(1 for p in proposals if p["tier"] == "green")
        yellow = sum(1 for p in proposals if p["tier"] == "yellow")
        red = sum(1 for p in proposals if p["tier"] == "red")
        lines.append("## Recipe proposals\n")
        lines.append("| Tier | Count |")
        lines.append("|---|---|")
        lines.append(f"| 🟢 Green (ready-for-review) | {green} |")
        lines.append(f"| 🟡 Yellow (needs-review)    | {yellow} |")
        lines.append(f"| 🔴 Red (manual)             | {red} |")
        lines.append(f"| **Total**                   | **{len(proposals)}** |")
        lines.append("")

    if s["deprecated"] > 0:
        lines.append("## Newly deprecated methods\n")
        for m in diff["deprecated"]:
            lines.append(f"- `{m['declaring_type']}.{m['name']}` — {m.get('obsolete_message') or '(no message)'}")
        lines.append("")

    if s["changed_signature"] > 0:
        lines.append("## Breaking changes — Signature changes\n")
        for change in diff["changed_signature"][:20]:
            before = change["before"]
            after = change["after"]
            lines.append(f"- `{after['declaring_type']}.{after['name']}`")
            lines.append(f"  - Before: `{before['signature']}`")
            lines.append(f"  - After:  `{after['signature']}`")
        if len(diff["changed_signature"]) > 20:
            lines.append(f"\n_(+ {len(diff['changed_signature']) - 20} more in `api-diff-{version}.json`)_")
        lines.append("")

    lines.append("## Artifacts\n")
    lines.append(f"- Methods: `data/api/{version}/methods.json`")
    lines.append(f"- Types:   `data/api/{version}/types.json`")
    lines.append(f"- Diff:    `{diff_path.name}`")
    if proposals_json_path:
        lines.append(f"- Proposals: `{proposals_json_path.name}`")
    lines.append("")

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(lines), encoding="utf-8")
