"""Canonical data model for the TopSolid API sync pipeline.

These dataclasses are the authoritative representation of TopSolid API
entities across the pipeline stages. Every stage consumes/produces instances
of these classes (serialized to JSON).

Keep this module dependency-free (stdlib only) so it can be imported from
anywhere in the pipeline without circular imports.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any


# ============================================================================
# Data classes
# ============================================================================

@dataclass
class ApiParameter:
    """A single parameter in a method signature."""
    name: str
    type: str
    description: str = ""
    is_out: bool = False
    is_ref: bool = False


@dataclass
class ApiMethod:
    """A method or property accessor from the TopSolid API.

    The primary key for diffing is (fully_qualified_name, normalized_signature).
    Two overloads of the same method share fully_qualified_name but differ in signature.
    """
    fully_qualified_name: str
    name: str
    declaring_type: str       # full interface/class FQN (e.g. "TopSolid.Kernel.Automating.IParameters")
    namespace: str
    signature: str            # raw C# from CHM (pretty-formatted, with parameter names)
    normalized_signature: str # for diff keying (whitespace collapsed, param names stripped)
    parameters: list[ApiParameter] = field(default_factory=list)
    return_type: str = "void"
    description: str = ""
    remarks: str = ""
    since: str | None = None           # e.g. "7.17.000.000"
    deprecated: bool = False
    obsolete_message: str | None = None
    source_file: str = ""              # relative .htm path, for traceability


@dataclass
class ApiType:
    """An interface, class, enum, struct, or delegate from the TopSolid API."""
    fully_qualified_name: str
    name: str
    namespace: str
    kind: str                          # "interface" | "class" | "enum" | "struct" | "delegate"
    members: list[str] = field(default_factory=list)  # FQNs of methods on this type
    description: str = ""
    since: str | None = None
    source_file: str = ""


# ============================================================================
# Serialization helpers
# ============================================================================

def method_to_dict(m: ApiMethod) -> dict[str, Any]:
    return asdict(m)


def dict_to_method(d: dict[str, Any]) -> ApiMethod:
    params = [ApiParameter(**p) for p in d.get("parameters", [])]
    return ApiMethod(
        fully_qualified_name=d["fully_qualified_name"],
        name=d["name"],
        declaring_type=d["declaring_type"],
        namespace=d["namespace"],
        signature=d["signature"],
        normalized_signature=d["normalized_signature"],
        parameters=params,
        return_type=d.get("return_type", "void"),
        description=d.get("description", ""),
        remarks=d.get("remarks", ""),
        since=d.get("since"),
        deprecated=d.get("deprecated", False),
        obsolete_message=d.get("obsolete_message"),
        source_file=d.get("source_file", ""),
    )


def type_to_dict(t: ApiType) -> dict[str, Any]:
    return asdict(t)


def dict_to_type(d: dict[str, Any]) -> ApiType:
    return ApiType(**d)


def write_json(path: Path, data: Any) -> None:
    """Write JSON with stable formatting (sorted keys, 2-space indent, UTF-8)."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(data, indent=2, ensure_ascii=False, sort_keys=False),
        encoding="utf-8",
    )


def load_methods(path: Path) -> list[ApiMethod]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    return [dict_to_method(d) for d in raw]


def load_types(path: Path) -> list[ApiType]:
    raw = json.loads(path.read_text(encoding="utf-8"))
    return [dict_to_type(d) for d in raw]


# ============================================================================
# Signature normalization (for diff keying)
# ============================================================================

_PARAM_NAME_RE = re.compile(r"\s+\b(in|out|ref)?\b\s*\b[a-z_][a-zA-Z0-9_]*(?=\s*[,)])", re.UNICODE)
_WHITESPACE_RE = re.compile(r"\s+")


def normalize_signature(signature: str) -> str:
    """Strip whitespace and parameter names so renaming `inId -> inElementId` doesn't count as a change.

    >>> normalize_signature("void Foo(int inId, string name)")
    'void Foo(int,string)'
    >>> normalize_signature("void Foo(int inElementId, string someName)")
    'void Foo(int,string)'
    """
    # Collapse whitespace first
    s = _WHITESPACE_RE.sub(" ", signature).strip()

    # Remove parameter names: find the parens, split by ",", drop the last word of each piece.
    def _strip_params(match: re.Match) -> str:
        inside = match.group(1)
        if not inside.strip():
            return "()"
        params = []
        # Split by comma, but not inside nested generics like List<T,U>
        depth = 0
        buf = []
        for c in inside:
            if c == "<":
                depth += 1
            elif c == ">":
                depth -= 1
            if c == "," and depth == 0:
                params.append("".join(buf))
                buf = []
            else:
                buf.append(c)
        if buf:
            params.append("".join(buf))

        # For each param, strip the trailing identifier (the param name)
        normalized_params = []
        for p in params:
            p = p.strip()
            if not p:
                continue
            # Strip leading modifiers like 'out', 'ref', 'in'
            # but keep them as prefix to the type
            # e.g. "out int value" -> "out int"
            tokens = p.split()
            if len(tokens) >= 2:
                # Drop the last token (param name)
                normalized_params.append(" ".join(tokens[:-1]))
            else:
                normalized_params.append(p)
        return "(" + ",".join(normalized_params) + ")"

    # Find first (...)
    paren = re.search(r"\((.*)\)", s, re.DOTALL)
    if not paren:
        return s
    before = s[: paren.start()]
    after = s[paren.end():]
    return before + _strip_params(paren) + after


# ============================================================================
# Method key (for diff)
# ============================================================================

def method_key(m: ApiMethod) -> str:
    """Key for diffing: FQN + normalized signature.

    Overloads differ in normalized_signature and get distinct keys.
    """
    return f"{m.fully_qualified_name}::{m.normalized_signature}"
