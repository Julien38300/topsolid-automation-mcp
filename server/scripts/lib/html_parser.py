"""Parse extracted CHM HTML pages into structured ApiMethod / ApiType records.

The parser relies on the Microsoft Help meta tags (authoritative) rather than
the HTML structure (which varies across Sandcastle versions). Non-reference
pages (namespaces, overload indexes, static lists) are ignored.

Public API:
    parse_directory(raw_dir: Path) -> (methods, types)
    parse_page(html_path: Path) -> ApiMethod | ApiType | None
"""
from __future__ import annotations

import re
from pathlib import Path

from bs4 import BeautifulSoup, Tag

from .api_model import ApiMethod, ApiParameter, ApiType, normalize_signature


# ---------------------------------------------------------------------------
# Regex helpers
# ---------------------------------------------------------------------------

# Microsoft.Help.Id takes forms:
#   T:Namespace.Type                          -> type
#   M:Namespace.Type.Method                   -> method (no params)
#   M:Namespace.Type.Method(param,param,...)  -> method (parameterized)
#   P:Namespace.Type.Property                 -> property
#   F:Namespace.Type.Field                    -> field
#   E:Namespace.Type.Event                    -> event
#   Overload:...                              -> overload group page (skip)

HELP_ID_RE = re.compile(
    r"^(?P<kind>[TMPFE]|Overload):(?P<body>.+)$",
    re.DOTALL,
)

# Parse "Method(Type1,Type2)" → method name + args string
METHOD_PARAMS_RE = re.compile(r"^(?P<name>[^(]+)(?:\((?P<params>.*)\))?$", re.DOTALL)

SINCE_RE = re.compile(r"available since v?(\d+(?:\.\d+){0,4})", re.IGNORECASE)
OBSOLETE_SIG_RE = re.compile(r"\[\s*Obsolete(?:Attribute)?\s*\(([^)]*)\)\s*\]", re.IGNORECASE)

# Type kind markers in page titles
TYPE_KIND_MARKERS = {
    "Interface": "interface",
    "Class": "class",
    "Enumeration": "enum",
    "Structure": "struct",
    "Delegate": "delegate",
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _meta(soup: BeautifulSoup, name: str) -> str | None:
    tag = soup.find("meta", attrs={"name": name})
    if tag and tag.get("content"):
        return tag["content"].strip()
    return None


def _text_of(tag: Tag | None) -> str:
    if tag is None:
        return ""
    # Strip script noise and inline branding markers
    for s in tag.find_all(["script", "style"]):
        s.decompose()
    return " ".join(tag.get_text(" ", strip=True).split())


def _split_type_and_member(fqn: str) -> tuple[str, str]:
    """Split "Namespace.Type.Method" -> ("Namespace.Type", "Method").

    Careful: some methods contain dots in generic parameters, but those are
    inside parens which we strip first.
    """
    last_dot = fqn.rfind(".")
    if last_dot == -1:
        return ("", fqn)
    return fqn[:last_dot], fqn[last_dot + 1:]


def _signature_block(soup: BeautifulSoup) -> str:
    """Return the first C# code signature on the page (raw text).

    The CHM embeds the signature under a div like:
        <div id="ID0EBCA_code_Div1"> <pre xml:space="preserve">...</pre> </div>
    The first <pre> under a codeSnippetContainer is the C# variant.
    """
    container = soup.find("div", class_="codeSnippetContainer")
    if container is None:
        return ""
    pre = container.find("pre")
    if pre is None:
        return ""
    # Strip inline script tags / non-text elements but keep code text
    for s in pre.find_all(["script", "style"]):
        s.decompose()
    return " ".join(pre.get_text(" ", strip=True).split())


def _remarks_text(soup: BeautifulSoup) -> str:
    """Find the Remarks section text, if any."""
    # Sandcastle uses collapsible sections with span title "Remarks"
    for title in soup.find_all("span", class_="collapsibleRegionTitle"):
        if title.get_text(strip=True) == "Remarks":
            # Next sibling div is the content
            section = title.find_parent("div").find_next_sibling(
                "div", class_="collapsibleSection"
            )
            if section:
                return _text_of(section)
    return ""


def _parameters_from_dl(soup: BeautifulSoup) -> list[tuple[str, str]]:
    """Extract (param_name, param_description) pairs from the 'Parameters' <dl>.

    Types are NOT reliably extracted from the dl because the HTML splits the
    type name across multiple <a> tags and inline script calls. We get the types
    from the C# signature instead; this function only provides names + descriptions.
    """
    # Find the h4 "Parameters" then the following <dl>
    pairs = []
    for h4 in soup.find_all("h4", class_="subHeading"):
        if h4.get_text(strip=True) == "Parameters":
            dl = h4.find_next("dl")
            if dl is None:
                break
            for dt in dl.find_all("dt"):
                span = dt.find("span", class_="parameter")
                if not span:
                    continue
                pname = span.get_text(strip=True)
                dd = dt.find_next("dd")
                pdesc = ""
                if dd:
                    # Description text is after the Type: ... <br /> marker.
                    # Take text after the last <br>; fallback to full text.
                    br_list = dd.find_all("br")
                    if br_list:
                        last_br = br_list[-1]
                        tail = []
                        for sib in last_br.next_siblings:
                            if isinstance(sib, Tag):
                                tail.append(_text_of(sib))
                            else:
                                tail.append(str(sib).strip())
                        pdesc = " ".join(t for t in tail if t)
                    else:
                        pdesc = _text_of(dd)
                pairs.append((pname, pdesc))
            break
    return pairs


# ---------------------------------------------------------------------------
# Signature parsing
# ---------------------------------------------------------------------------

# After stripping attrs and modifiers, match return type + name + (... args ...)
SIG_HEAD_RE = re.compile(
    r"^\s*(?P<return>[\w\.<>\[\]\s,?]*?)\s+"  # return type (non-greedy)
    r"(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*"    # method name
    r"(?:<[^>]*>)?\s*"                        # optional generics <T, U>
    r"\(",                                     # opening paren
    re.DOTALL,
)

# Leading attribute / modifier tokens we strip before main parse
_MODIFIERS = {
    "public", "private", "protected", "internal",
    "static", "sealed", "virtual", "override", "abstract",
    "readonly", "extern", "new", "unsafe", "async", "partial",
}
_ATTR_RE = re.compile(r"^\s*\[[^\]]*\]\s*")


def _strip_attrs_and_modifiers(signature: str) -> str:
    """Remove leading [Attribute(...)] blocks and visibility/modifier keywords.

    >>> _strip_attrs_and_modifiers('[Obsolete("x")] public static void Foo()')
    'void Foo()'
    """
    s = signature
    # Strip leading attribute brackets repeatedly
    while True:
        m = _ATTR_RE.match(s)
        if not m:
            break
        s = s[m.end():]
    s = s.lstrip()

    # Strip leading modifier tokens repeatedly
    while True:
        # Read first token
        mt = re.match(r"\s*(\w+)\s+", s)
        if not mt:
            break
        tok = mt.group(1)
        if tok not in _MODIFIERS:
            break
        s = s[mt.end():]
    return s


def _parse_signature_c_sharp(signature: str) -> tuple[str, list[tuple[str, str, bool, bool]]]:
    """Parse a C# method signature string.

    Returns (return_type, [(param_name, param_type, is_out, is_ref), ...]).
    For constructors, return_type is the class name.
    Returns ("", []) if the signature can't be parsed.
    """
    clean = _strip_attrs_and_modifiers(signature)
    m = SIG_HEAD_RE.match(clean)

    if m:
        return_type = (m.group("return") or "void").strip() or "void"
        paren_pos = m.end()
    else:
        # Might be a constructor with no return type: `ClassName(params)`
        ctor_re = re.compile(r"^\s*(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(")
        mc = ctor_re.match(clean)
        if mc:
            return_type = mc.group("name")
            paren_pos = mc.end()
        else:
            return "", []

    # Find matching close paren, accounting for nested generics
    depth = 1
    i = paren_pos
    while i < len(clean) and depth > 0:
        c = clean[i]
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                break
        i += 1
    if depth != 0:
        return return_type, []

    args_raw = clean[paren_pos:i]
    params: list[tuple[str, str, bool, bool]] = []
    if args_raw.strip():
        for chunk in _split_params(args_raw):
            chunk = chunk.strip()
            if not chunk:
                continue
            is_out, is_ref = False, False
            tokens = chunk.split()
            if tokens and tokens[0] == "out":
                is_out = True
                tokens = tokens[1:]
            elif tokens and tokens[0] == "ref":
                is_ref = True
                tokens = tokens[1:]
            elif tokens and tokens[0] == "in":
                tokens = tokens[1:]
            if len(tokens) < 2:
                # Malformed — skip
                continue
            pname = tokens[-1]
            ptype = " ".join(tokens[:-1])
            params.append((pname, ptype, is_out, is_ref))

    return return_type, params


def _split_params(s: str) -> list[str]:
    """Split a parameter list by commas, respecting nested <> and []."""
    depth = 0
    out = []
    buf: list[str] = []
    for c in s:
        if c in "<[":
            depth += 1
        elif c in ">]":
            depth -= 1
        if c == "," and depth == 0:
            out.append("".join(buf))
            buf = []
        else:
            buf.append(c)
    if buf:
        out.append("".join(buf))
    return out


# ---------------------------------------------------------------------------
# Page parsers
# ---------------------------------------------------------------------------

def _parse_method_page(soup: BeautifulSoup, source_file: str, help_id_body: str) -> ApiMethod | None:
    """Parse a method reference page.

    `help_id_body` is the body after "M:" — e.g. "TopSolid.X.IFoo.Bar(TopSolid.X.ElementId)".
    """
    # Separate the "method path" and the "(params)" part for clean FQN.
    paren_idx = help_id_body.find("(")
    method_path = help_id_body[:paren_idx] if paren_idx != -1 else help_id_body
    fqn = method_path.strip()

    declaring_type, method_name = _split_type_and_member(fqn)
    namespace = _meta(soup, "container") or ""

    description = _meta(soup, "Description") or ""
    signature = _signature_block(soup)
    remarks = _remarks_text(soup)

    return_type, sig_params = _parse_signature_c_sharp(signature)

    # Merge descriptions from the parameters <dl> into the signature-derived params
    dl_descs = {name: desc for name, desc in _parameters_from_dl(soup)}
    parameters = []
    for pname, ptype, is_out, is_ref in sig_params:
        parameters.append(
            ApiParameter(
                name=pname,
                type=ptype,
                description=dl_descs.get(pname, ""),
                is_out=is_out,
                is_ref=is_ref,
            )
        )

    # Detect deprecated
    obsolete_msg = None
    deprecated = False
    if signature:
        obs_match = OBSOLETE_SIG_RE.search(signature)
        if obs_match:
            deprecated = True
            inner = obs_match.group(1).strip()
            if inner:
                # Strip surrounding quotes if any
                obsolete_msg = inner.strip("\"'")
    if not deprecated and remarks:
        if "obsolete" in remarks.lower() or "deprecated" in remarks.lower():
            deprecated = True

    # Detect "since" version
    since = None
    for src in (remarks, description, signature):
        if src:
            m = SINCE_RE.search(src)
            if m:
                since = m.group(1)
                break

    return ApiMethod(
        fully_qualified_name=fqn,
        name=method_name,
        declaring_type=declaring_type,
        namespace=namespace,
        signature=signature,
        normalized_signature=normalize_signature(signature) if signature else "",
        parameters=parameters,
        return_type=return_type,
        description=description,
        remarks=remarks,
        since=since,
        deprecated=deprecated,
        obsolete_message=obsolete_msg,
        source_file=source_file,
    )


def _parse_type_page(soup: BeautifulSoup, source_file: str, help_id_body: str) -> ApiType | None:
    """Parse a type (interface/class/enum/struct/delegate) reference page."""
    fqn = help_id_body.strip()
    namespace = _meta(soup, "container") or ""
    _, type_name = _split_type_and_member(fqn)
    description = _meta(soup, "Description") or ""

    # Determine kind from the page title, which ends with " Interface", " Class", etc.
    title_tag = soup.find("title")
    kind = "class"
    if title_tag:
        title = title_tag.get_text(strip=True)
        for marker, mapped in TYPE_KIND_MARKERS.items():
            if title.endswith(marker):
                kind = mapped
                break

    # "Since" from remarks if present
    remarks = _remarks_text(soup)
    since = None
    m = SINCE_RE.search(remarks or "")
    if m:
        since = m.group(1)

    return ApiType(
        fully_qualified_name=fqn,
        name=type_name,
        namespace=namespace,
        kind=kind,
        members=[],  # filled in a second pass from ApiMethod.declaring_type
        description=description,
        since=since,
        source_file=source_file,
    )


def parse_page(html_path: Path) -> ApiMethod | ApiType | None:
    """Parse a single .htm page. Returns None for non-reference pages."""
    try:
        text = html_path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return None
    soup = BeautifulSoup(text, "lxml")

    help_id = _meta(soup, "Microsoft.Help.Id")
    if not help_id:
        return None

    m = HELP_ID_RE.match(help_id)
    if not m:
        return None

    kind = m.group("kind")
    body = m.group("body")

    # Only index methods (M) and types (T). Skip overload groups / properties / etc. for v1.
    # (Properties and fields could be added later — they're much rarer.)
    rel_path = html_path.name  # just the filename, for traceability
    if kind == "M":
        return _parse_method_page(soup, rel_path, body)
    elif kind == "T":
        return _parse_type_page(soup, rel_path, body)
    return None


# ---------------------------------------------------------------------------
# Directory parser
# ---------------------------------------------------------------------------

def parse_directory(raw_dir: Path) -> tuple[list[ApiMethod], list[ApiType]]:
    """Parse all .htm files under raw_dir/html/ (or raw_dir/)."""
    html_dir = raw_dir / "html"
    if not html_dir.exists():
        html_dir = raw_dir

    methods: list[ApiMethod] = []
    types: list[ApiType] = []
    skipped = 0

    htm_files = sorted(html_dir.glob("*.htm"))
    for i, htm in enumerate(htm_files):
        result = parse_page(htm)
        if isinstance(result, ApiMethod):
            methods.append(result)
        elif isinstance(result, ApiType):
            types.append(result)
        else:
            skipped += 1

    # Populate ApiType.members from collected methods
    type_index = {t.fully_qualified_name: t for t in types}
    for m in methods:
        if m.declaring_type in type_index:
            type_index[m.declaring_type].members.append(m.fully_qualified_name)

    # Sort members for stable output
    for t in types:
        t.members.sort()

    # Sort results for stable output (deterministic diffing)
    methods.sort(key=lambda x: (x.fully_qualified_name, x.normalized_signature))
    types.sort(key=lambda x: x.fully_qualified_name)

    return methods, types
