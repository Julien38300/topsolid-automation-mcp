"""Propose new recipes from CHM-only methods (CHM methods not yet in graph edges).

Classification tiers:
  - Green:  simple getters with scalar return (safe, ready-to-review)
  - Yellow: transactional (StartModification mentioned, or Set*/Create*/Remove* name)
  - Red:    unclear (out/ref params, generics, callbacks, deprecated)

Output: markdown file for human review + machine-readable JSON.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal


# Return types considered "simple" for Green tier
SIMPLE_SCALAR_RETURN_TYPES = {
    "int", "Int32", "long", "Int64", "double", "Double",
    "float", "Single", "bool", "Boolean",
    "string", "String",
    "ElementId", "DocumentId", "PdmObjectId", "ProjectId",
    "Guid",
    "DateTime",
}

# Method name prefixes that hint at write semantics
WRITE_PREFIXES = ("Set", "Create", "Remove", "Delete", "Add", "Insert", "Clear", "Update")

# Method name prefixes that hint at read semantics
READ_PREFIXES = ("Get", "Read", "List", "Find", "Is", "Has", "Count")


@dataclass
class RecipeProposal:
    tier: Literal["green", "yellow", "red"]
    method_fqn: str
    declaring_type: str
    method_name: str
    proposed_recipe_name: str
    description: str
    remarks: str
    since: str | None
    signature: str
    parameters: list[dict[str, Any]]
    return_type: str
    recipe_code: str
    status: str  # "ready-for-review" | "needs-review" | "manual"
    tags: list[str] = field(default_factory=list)


def _snake_case(name: str) -> str:
    """CamelCase -> snake_case. `ReadMassVolume` -> `read_mass_volume`."""
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def _propose_recipe_name(method_name: str) -> str:
    """Generate a recipe name like `read_designation` from a method name `GetDesignation`."""
    # Drop common Get/Read prefix → just snake_case the rest
    # But for Set/Create/etc. keep it
    return _snake_case(method_name)


def _classify(method: dict[str, Any]) -> tuple[str, list[str]]:
    """Return (tier, tags)."""
    tags = []
    name = method.get("name", "")
    params = method.get("parameters", []) or []
    return_type = method.get("return_type", "").strip()
    remarks = method.get("remarks", "") or ""
    deprecated = method.get("deprecated", False)

    # Deprecated → Red
    if deprecated:
        tags.append("deprecated")
        return "red", tags

    # out/ref params → Red
    has_out_ref = any(p.get("is_out") or p.get("is_ref") for p in params)
    if has_out_ref:
        tags.append("out-ref-params")
        return "red", tags

    # Generic T → Red
    if "<T" in method.get("signature", "") or "<T>" in return_type:
        tags.append("generic")
        return "red", tags

    # Transactional → Yellow
    is_transactional = (
        "StartModification" in remarks
        or "EnsureIsDirty" in remarks
        or name.startswith(WRITE_PREFIXES)
    )
    if is_transactional:
        tags.append("transactional")
        return "yellow", tags

    # Simple getter with scalar return → Green
    is_simple_read = (
        name.startswith(READ_PREFIXES)
        and len(params) <= 1
        and return_type.split()[0] if return_type else ""
    )
    if is_simple_read:
        # Check return type is in the simple set
        rt_root = return_type.split("<")[0].strip()
        if rt_root in SIMPLE_SCALAR_RETURN_TYPES:
            tags.append("simple-getter")
            return "green", tags

    # Default → Red (unclear)
    tags.append("unclear")
    return "red", tags


def _generate_green_recipe(method: dict[str, Any], recipe_name: str) -> str:
    """Template for a simple read recipe (Green tier).

    Uses a fictitious `TopSolidHost.<category>` pattern — the human reviewer
    will know which interface host-accessor to use.
    """
    params = method.get("parameters", []) or []
    name = method.get("name", "")
    decl = method.get("declaring_type", "")
    interface_short = decl.rsplit(".", 1)[-1]

    # Guess a host accessor (the human will fix it)
    host_prop = interface_short.lstrip("I")
    # e.g. IParameters -> Parameters

    args_list = []
    for p in params:
        pname = p.get("name", "arg")
        args_list.append(pname)

    arg_str = ", ".join(args_list) if args_list else ""

    code = f"""// Proposed READ recipe: {recipe_name}
// Description: {method.get('description', '')}
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
var result = TopSolidHost.{host_prop}.{name}({arg_str});
return result.ToString();"""
    return code


def _generate_yellow_recipe(method: dict[str, Any], recipe_name: str) -> str:
    """Template for a transactional write recipe (Yellow tier, Pattern D)."""
    params = method.get("parameters", []) or []
    name = method.get("name", "")
    decl = method.get("declaring_type", "")
    interface_short = decl.rsplit(".", 1)[-1]
    host_prop = interface_short.lstrip("I")

    # Use placeholders for parameter values the user will fill in via {value}
    param_sigs = []
    for p in params:
        pname = p.get("name", "arg")
        param_sigs.append(f"{pname} /* TODO: {p.get('type')} */")

    arg_str = ", ".join(param_sigs) if param_sigs else ""

    code = f"""// Proposed WRITE recipe: {recipe_name} (Pattern D)
// Description: {method.get('description', '')}
DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
TopSolidHost.Application.StartModification("{recipe_name}", false);
try {{
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // NOTE: docId may have CHANGED after EnsureIsDirty — re-fetch dependent IDs here.
    TopSolidHost.{host_prop}.{name}({arg_str});
    TopSolidHost.Application.EndModification(true, true);
}} catch (Exception ex) {{
    TopSolidHost.Application.EndModification(false, false);
    return "ERROR: " + ex.Message;
}}
PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
TopSolidHost.Pdm.Save(pdmId, true);
return "OK: {recipe_name}";"""
    return code


def _generate_red_recipe(method: dict[str, Any], recipe_name: str, tags: list[str]) -> str:
    """Red tier — stub only, asking the human to write it manually."""
    reason = ", ".join(tags)
    return f"""// TODO: manual review needed ({reason})
// Method: {method.get('declaring_type', '')}.{method.get('name', '')}
// Signature: {method.get('signature', '')}
// Description: {method.get('description', '')}
return "TODO: implement {recipe_name}";"""


def propose_for_method(method: dict[str, Any]) -> RecipeProposal:
    name = method.get("name", "")
    recipe_name = _propose_recipe_name(name)

    tier, tags = _classify(method)

    if tier == "green":
        code = _generate_green_recipe(method, recipe_name)
        status = "ready-for-review"
    elif tier == "yellow":
        code = _generate_yellow_recipe(method, recipe_name)
        status = "needs-review"
    else:
        code = _generate_red_recipe(method, recipe_name, tags)
        status = "manual"

    return RecipeProposal(
        tier=tier,
        method_fqn=method.get("fully_qualified_name", ""),
        declaring_type=method.get("declaring_type", ""),
        method_name=name,
        proposed_recipe_name=recipe_name,
        description=method.get("description", ""),
        remarks=method.get("remarks", ""),
        since=method.get("since"),
        signature=method.get("signature", ""),
        parameters=method.get("parameters", []) or [],
        return_type=method.get("return_type", ""),
        recipe_code=code,
        status=status,
        tags=tags,
    )


def _should_skip(method: dict[str, Any]) -> bool:
    """Filter out methods that are never useful recipe candidates.

    - #ctor (constructors) — irrelevant for scripting via MCP
    - op_* (operator overloads: op_Equality, op_Addition, etc.)
    - Equals, GetHashCode, ToString (inherited from object)
    """
    name = method.get("name", "")
    if name in {"#ctor", "Equals", "GetHashCode", "ToString"}:
        return True
    if name.startswith("op_"):
        return True
    return False


def propose_recipes(chm_only_methods: list[dict[str, Any]]) -> list[RecipeProposal]:
    """Classify and generate proposals for each CHM-only method (excluding noise)."""
    return [propose_for_method(m) for m in chm_only_methods if not _should_skip(m)]


def write_proposals_md(proposals: list[RecipeProposal], out_path: Path, version: str) -> None:
    """Write a markdown file for human review."""
    # Sort: green first, then yellow, then red; alphabetic within tier
    tier_order = {"green": 0, "yellow": 1, "red": 2}
    proposals_sorted = sorted(
        proposals,
        key=lambda p: (tier_order.get(p.tier, 9), p.declaring_type, p.method_name),
    )

    green_count = sum(1 for p in proposals_sorted if p.tier == "green")
    yellow_count = sum(1 for p in proposals_sorted if p.tier == "yellow")
    red_count = sum(1 for p in proposals_sorted if p.tier == "red")

    lines = []
    lines.append(f"# Recipe Proposals — TopSolid {version}\n")
    lines.append(f"Generated from `data/api/{version}/methods.json` — CHM-only methods")
    lines.append(f"(those not found in the current `graph.json` by (Interface, MethodName) key).\n")
    lines.append(f"## Summary\n")
    lines.append(f"| Tier | Count | Description |")
    lines.append(f"|---|---|---|")
    lines.append(f"| 🟢 Green  | {green_count} | Simple read, scalar return — ready for review |")
    lines.append(f"| 🟡 Yellow | {yellow_count} | Transactional / Write — Pattern D skeleton, needs review |")
    lines.append(f"| 🔴 Red    | {red_count} | Unclear or risky — manual implementation |")
    lines.append(f"| **Total** | **{len(proposals_sorted)}** | |")
    lines.append("")

    for tier_letter, tier_name, tier_emoji in [
        ("green", "Green — Ready for review", "🟢"),
        ("yellow", "Yellow — Needs review", "🟡"),
        ("red", "Red — Manual", "🔴"),
    ]:
        tier_proposals = [p for p in proposals_sorted if p.tier == tier_letter]
        if not tier_proposals:
            continue
        lines.append(f"\n## {tier_emoji} {tier_name} ({len(tier_proposals)})\n")
        for p in tier_proposals:
            lines.append(f"### `{p.proposed_recipe_name}` — `{p.declaring_type}.{p.method_name}`\n")
            if p.since:
                lines.append(f"**Since:** v{p.since}  ")
            if p.description:
                lines.append(f"**Description:** {p.description}  ")
            if p.remarks:
                lines.append(f"**Remarks:** {p.remarks}  ")
            lines.append(f"**Tags:** `{', '.join(p.tags)}`  \n")
            lines.append(f"**Signature:**")
            lines.append(f"```csharp")
            lines.append(f"{p.signature}")
            lines.append(f"```\n")
            lines.append(f"**Proposed recipe code:**")
            lines.append(f"```csharp")
            lines.append(p.recipe_code)
            lines.append(f"```\n")

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text("\n".join(lines), encoding="utf-8")


def write_proposals_json(proposals: list[RecipeProposal], out_path: Path) -> None:
    """Write machine-readable proposals for downstream tools."""
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        json.dumps(
            [p.__dict__ for p in proposals],
            indent=2, ensure_ascii=False,
        ),
        encoding="utf-8",
    )
