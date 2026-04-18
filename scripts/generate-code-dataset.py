#!/usr/bin/env python3
"""Build the Phase 9b training dataset for Codestral-22B TopSolid code agent.

Sources & targets (~2500 examples total):
  1. RecipeTool.cs       — 118 recipes -> ~300 entries (2-3 variants each)
  2. methods.json        — 502 StartModification methods -> ~900 synthetic Pattern D entries
  3. AF corpus           -> ~250 entries (method-level chunks)
  4. RoB corpus          -> ~700 entries (method-level chunks, deduped)
  5. SI-units positives  — ~100 entries (explicit mm/deg conversions)
  6. Error handling      — ~100 entries (try/catch with EndModification(false,false))
  7. Refusals            — ~100 entries (out-of-scope -> polite refusal)

Output format: ShareGPT JSONL with [system, user, assistant] messages.
Assistant content = C# code wrapped in ```csharp fence.

Usage:
  python scripts/generate-code-dataset.py
"""
from __future__ import annotations

import json
import random
import re
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).parent
PROJECT_DIR = SCRIPT_DIR.parent
DATA_DIR = PROJECT_DIR / "data"
RECIPES_CS = PROJECT_DIR / "src" / "Tools" / "RecipeTool.cs"
METHODS_JSON = DATA_DIR / "api" / "7.21.164.0" / "methods.json"
OUT_FILE = DATA_DIR / "code-dataset.jsonl"
STATS_FILE = DATA_DIR / "code-dataset-stats.json"

AF_DIR = Path(r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples REDACTED-USER")
ROB_DIR = Path(r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples RoB")
FEA_DIR = Path(r"C:\Users\jup\OneDrive\4 - VB\Projects\Script Qualité FEA")  # FEA Quality automation — 34 files, prod-grade


SYSTEM_PROMPT = """You are a TopSolid C# code generator. Given a user request, emit a self-contained C# script body (no namespace, no class, no `using` — these are auto-injected). The code runs inside a `Run()` method that must return a string.

CRITICAL RULES:
1. ALL writes MUST follow Pattern D:
   TopSolidHost.Application.StartModification("label", false);
   try {
       TopSolidHost.Documents.EnsureIsDirty(ref docId);  // docId CHANGES here!
       // re-fetch dependent IDs AFTER this line (NOT before)
       /* mutations */
       TopSolidHost.Application.EndModification(true, true);
       PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
       TopSolidHost.Pdm.Save(pdm, true);
       return "OK";
   } catch (Exception ex) {
       TopSolidHost.Application.EndModification(false, false);
       return "ERROR: " + ex.Message;
   }

2. SI UNITS: TopSolid uses meters and radians. Convert:
   mm → value * 0.001 (e.g. 50mm = 0.05)
   deg → value * Math.PI / 180.0 (e.g. 45deg = 0.7854)
   When OUTPUTTING: multiply by 1000 for mm, by 180/PI for deg

3. Get the document:
   DocumentId docId = TopSolidHost.Documents.EditedDocument;
   if (docId.IsEmpty) return "No document open.";

4. Only call TopSolidHost.* APIs that exist. Do NOT invent methods.

5. For out-of-scope requests (email, Excel, git, FTP, etc.), return:
   return "Cannot do that — not a TopSolid task.";

Output ONLY the C# code body, wrapped in ```csharp ... ``` fences. No explanation."""


# ============================================================================
# HELPERS
# ============================================================================

def make_entry(user_msg: str, assistant_code: str, tag: str = "") -> dict[str, Any]:
    """Create a ShareGPT entry. assistant_code should NOT include the fence."""
    code_wrapped = f"```csharp\n{assistant_code.strip()}\n```"
    e = {
        "conversations": [
            {"from": "system", "value": SYSTEM_PROMPT},
            {"from": "human", "value": user_msg},
            {"from": "gpt", "value": code_wrapped},
        ]
    }
    if tag:
        e["tag"] = tag
    return e


# ============================================================================
# SOURCE 1 — RecipeTool.cs recipes
# ============================================================================

RECIPE_BLOCK_RE = re.compile(
    r'\{\s*"(?P<name>[a-z_]+)",\s*(?P<mode>R|RW)\("(?P<desc>[^"]*)",\s*\n(?P<body>(?:.|\n)*?)\)\s*\}',
    re.DOTALL,
)


def extract_recipes() -> list[dict[str, str]]:
    """Parse RecipeTool.cs and return list of {name, description, code}."""
    if not RECIPES_CS.exists():
        return []
    text = RECIPES_CS.read_text(encoding="utf-8")
    out = []
    for m in RECIPE_BLOCK_RE.finditer(text):
        name = m.group("name")
        desc = m.group("desc")
        # body contains concatenated C# strings — parse by extracting stripped lines
        body_raw = m.group("body")
        # Extract strings between quotes, concat, unescape
        string_parts = re.findall(r'"((?:[^"\\]|\\.)*)"', body_raw)
        code = "".join(s.encode().decode("unicode_escape") for s in string_parts)
        if not code.strip():
            continue
        out.append({"name": name, "description": desc, "code": code.strip()})
    return out


def generate_from_recipes(recipes: list[dict[str, str]]) -> list[dict[str, Any]]:
    """For each recipe, generate 2-3 user-request variants → same code."""
    entries = []
    random.seed(42)
    for r in recipes:
        name = r["name"]
        desc = r["description"]
        code = r["code"]

        # Variant 1: FR description reformulated
        fr_prompts = [
            f"écris un script qui {desc.lower()[0:1].lower() + desc[1:] if desc else 'fait cela'}",
            f"{desc.lower()} — donne-moi le code C#",
            f"je veux {desc.lower()[0:1].lower() + desc[1:] if desc else 'faire cela'} en C#",
        ]
        # EN description variant
        en_prompts = [
            f"{desc}",
            f"write a C# script that {desc.lower()[0:1].lower() + desc[1:] if desc else 'does that'}",
        ]

        for p in random.sample(fr_prompts, 2) + random.sample(en_prompts, 1):
            entries.append(make_entry(p, code, tag=f"recipe:{name}"))

    return entries


# ============================================================================
# SOURCE 2 — Synthetic Pattern D from methods.json
# ============================================================================

def generate_synthetic_pattern_d(methods: list[dict[str, Any]], n_target: int = 600) -> list[dict[str, Any]]:
    """For each StartModification method, synthesize a Pattern D example."""
    transactional = [
        m for m in methods
        if m.get("remarks") and "StartModification" in m["remarks"]
        and not m.get("deprecated")
    ]
    random.seed(123)
    random.shuffle(transactional)
    transactional = transactional[:n_target]

    entries = []
    for m in transactional:
        name = m["name"]
        decl = m["declaring_type"]
        host_prop = decl.rsplit(".", 1)[-1].lstrip("I")  # IParameters -> Parameters
        params = m.get("parameters", []) or []

        # Build a user request
        desc = m.get("description") or f"Call {name}"
        fr_prompt = f"{desc.rstrip('.')}. Génère un script Pattern D."
        en_prompt = f"{desc} Generate a Pattern D script."

        # Build the code body
        arg_list = []
        for p in params:
            pname = p.get("name", "arg")
            ptype = p.get("type", "object")
            # Use placeholder variable
            arg_list.append(pname)

        args_str = ", ".join(arg_list) if arg_list else ""
        # Declare placeholder parameters as comments (the real values come from runtime)
        param_decls = "\n    ".join(
            f"// TODO: {p.get('name', 'arg')} of type {p.get('type', 'object')}"
            for p in params
        )

        code = f"""DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";

TopSolidHost.Application.StartModification("{name}", false);
try {{
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // docId may have changed — re-fetch dependent IDs here
    {param_decls}
    TopSolidHost.{host_prop}.{name}({args_str});
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdm, true);
    return "OK: {name} done.";
}} catch (Exception ex) {{
    TopSolidHost.Application.EndModification(false, false);
    return "ERROR: " + ex.Message;
}}"""
        entries.append(make_entry(fr_prompt, code, tag=f"pattern_d:{name}"))
        entries.append(make_entry(en_prompt, code, tag=f"pattern_d:{name}"))

    return entries


# ============================================================================
# SOURCE 3 & 4 — AF and RoB corpora (method-level chunks)
# ============================================================================

METHOD_RE = re.compile(
    r"(?:public|private|protected|internal|static|\s)+\s+\w[\w\.<>,\[\]]*\s+"
    r"(?P<name>[A-Z][a-zA-Z0-9_]+)\s*\([^)]*\)\s*(?:where[^{]+)?\{",
    re.MULTILINE,
)


def extract_methods_from_cs(path: Path) -> list[dict[str, str]]:
    """Extract method bodies from a C# file via brace-matching.

    Returns list of {method_name, body, file}.
    """
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return []

    if "TopSolid" not in text:
        return []

    results = []
    for m in METHOD_RE.finditer(text):
        start = m.end() - 1  # position of opening brace
        # Brace-match
        depth = 0
        i = start
        while i < len(text):
            c = text[i]
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0:
                    body = text[start + 1:i].strip()
                    if 5 <= body.count("\n") <= 120 and "TopSolid" in body:
                        results.append({
                            "method_name": m.group("name"),
                            "body": body,
                            "file": str(path.name),
                        })
                    break
            i += 1
    return results


def simple_back_gen(method: dict[str, str]) -> str:
    """Generate a heuristic user request from a method body.

    For offline dataset gen, we keep it simple: use method name as a hint.
    In production, this would call Main 14B via Ollama for better quality.
    """
    name = method["method_name"]
    # CamelCase → sentence
    words = re.sub(r"(?<=[a-z])(?=[A-Z])", " ", name).lower()
    return f"écris un script C# pour : {words}"


def extract_from_corpus(root: Path, n_target: int) -> list[dict[str, Any]]:
    if not root.exists():
        return []

    cs_files = [
        p for p in root.rglob("*.cs")
        if "bin" not in p.parts and "obj" not in p.parts and ".vs" not in p.parts
    ]

    all_methods = []
    for f in cs_files:
        all_methods.extend(extract_methods_from_cs(f))

    random.seed(7)
    random.shuffle(all_methods)
    all_methods = all_methods[:n_target]

    entries = []
    seen_bodies = set()
    for m in all_methods:
        body = m["body"]
        # Dedup exact body
        body_hash = hash(body)
        if body_hash in seen_bodies:
            continue
        seen_bodies.add(body_hash)

        prompt = simple_back_gen(m)
        entries.append(make_entry(prompt, body, tag=f"corpus:{m['file']}:{m['method_name']}"))
    return entries


# ============================================================================
# SOURCE 5 — SI-units positives
# ============================================================================

SI_EXAMPLES = [
    ("mets le parametre Longueur a 50mm", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
TopSolidHost.Application.StartModification("Set Length", false);
try {
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    var p = TopSolidHost.Parameters.GetParameter(docId, "Longueur");
    double meters = 50.0 * 0.001;  // 50mm -> 0.05m
    TopSolidHost.Parameters.SetRealParameterValue(p, meters);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdm, true);
    return "OK: Longueur = 50mm";
} catch (Exception ex) {
    TopSolidHost.Application.EndModification(false, false);
    return "ERROR: " + ex.Message;
}"""),
    ("cree un point 3D a (100mm, 50mm, 20mm)", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
TopSolidHost.Application.StartModification("Create 3D point", false);
try {
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    double x = 100.0 * 0.001;
    double y = 50.0 * 0.001;
    double z = 20.0 * 0.001;
    var point = TopSolidHost.Geometries3D.CreatePoint3D(docId, new Point3D(x, y, z));
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdm, true);
    return "OK: point created at (100,50,20) mm";
} catch (Exception ex) {
    TopSolidHost.Application.EndModification(false, false);
    return "ERROR: " + ex.Message;
}"""),
    ("rotation de 45 degres autour de Z", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
double angleRad = 45.0 * Math.PI / 180.0;  // 45 deg -> rad
return "Angle in radians: " + angleRad.ToString("F4");"""),
    ("donne le volume de la piece en cm3", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
double volumeM3 = TopSolidHost.Parts.GetVolume(docId);
double volumeCm3 = volumeM3 * 1e6;  // m3 -> cm3
return "Volume: " + volumeCm3.ToString("F2") + " cm3";"""),
    ("masse de la piece en kg", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
double massKg = TopSolidHost.Parts.GetMass(docId);
return "Mass: " + massKg.ToString("F3") + " kg";"""),
    ("diametre du trou a 12.5mm", """DocumentId docId = TopSolidHost.Documents.EditedDocument;
if (docId.IsEmpty) return "No document open.";
TopSolidHost.Application.StartModification("Set hole diameter", false);
try {
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    double diamMeters = 12.5 * 0.001;
    var p = TopSolidHost.Parameters.GetParameter(docId, "Diametre");
    TopSolidHost.Parameters.SetRealParameterValue(p, diamMeters);
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdm = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdm, true);
    return "OK: Diametre = 12.5mm";
} catch (Exception ex) {
    TopSolidHost.Application.EndModification(false, false);
    return "ERROR: " + ex.Message;
}"""),
]


def generate_si_units() -> list[dict[str, Any]]:
    return [make_entry(p, c, tag="si_units") for (p, c) in SI_EXAMPLES]


# ============================================================================
# SOURCE 6 — Refusals (out-of-scope)
# ============================================================================

REFUSAL_PROMPTS = [
    "envoie un email a julien@exemple.com avec le rapport",
    "send an email with the part details",
    "ouvre Excel et insere les parametres en colonne A",
    "open Excel and paste parameters in column A",
    "fais un git commit des modifications",
    "git commit the changes",
    "telecharge la derniere version depuis un serveur FTP",
    "download latest from an FTP server",
    "lance une simulation FEA sur la piece",
    "run FEA simulation on the part",
    "ecris un programme Python qui calcule pi",
    "write a Python program that computes pi",
    "connecte-toi a la base SQL Server et insere les donnees",
    "connect to SQL Server and insert data",
    "fais un cafe",
    "make me coffee",
    "cherche sur Google des pieces similaires",
    "google for similar parts",
    "imprime la piece sur l'imprimante 3D",
    "print the part on the 3D printer",
]


def generate_refusals() -> list[dict[str, Any]]:
    code = 'return "Cannot do that — not a TopSolid task.";'
    return [make_entry(p, code, tag="refusal") for p in REFUSAL_PROMPTS]


# ============================================================================
# MAIN
# ============================================================================

def main() -> None:
    print("=== TopSolid Code Agent Dataset Builder ===\n")
    all_entries: list[dict[str, Any]] = []

    # 1. Recipes
    recipes = extract_recipes()
    print(f"Recipes parsed from RecipeTool.cs: {len(recipes)}")
    recipe_entries = generate_from_recipes(recipes)
    print(f"  -> {len(recipe_entries)} dataset entries")
    all_entries.extend(recipe_entries)

    # 2. Synthetic Pattern D
    if METHODS_JSON.exists():
        methods = json.loads(METHODS_JSON.read_text(encoding="utf-8"))
        pd_entries = generate_synthetic_pattern_d(methods, n_target=450)
        print(f"Synthetic Pattern D entries: {len(pd_entries)}")
        all_entries.extend(pd_entries)
    else:
        print(f"WARNING: {METHODS_JSON} not found, skipping synthetic Pattern D")

    # 3. AF corpus
    af_entries = extract_from_corpus(AF_DIR, n_target=200)
    print(f"AF corpus entries: {len(af_entries)}")
    all_entries.extend(af_entries)

    # 4. RoB corpus
    rob_entries = extract_from_corpus(ROB_DIR, n_target=600)
    print(f"RoB corpus entries: {len(rob_entries)}")
    all_entries.extend(rob_entries)

    # 4b. FEA Quality corpus (prod-grade TopSolid+Excel automation)
    fea_entries = extract_from_corpus(FEA_DIR, n_target=150)
    print(f"FEA Quality corpus entries: {len(fea_entries)}")
    all_entries.extend(fea_entries)

    # 5. SI units positives
    si_entries = generate_si_units()
    print(f"SI-units entries: {len(si_entries)}")
    all_entries.extend(si_entries)

    # 6. Refusals
    ref_entries = generate_refusals()
    print(f"Refusal entries: {len(ref_entries)}")
    all_entries.extend(ref_entries)

    # Shuffle & write
    random.seed(42)
    random.shuffle(all_entries)

    OUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    with open(OUT_FILE, "w", encoding="utf-8") as f:
        for e in all_entries:
            f.write(json.dumps(e, ensure_ascii=False) + "\n")

    # Stats
    by_tag = {}
    for e in all_entries:
        tag_prefix = e.get("tag", "").split(":")[0] or "untagged"
        by_tag[tag_prefix] = by_tag.get(tag_prefix, 0) + 1

    stats = {
        "total": len(all_entries),
        "by_source": by_tag,
        "output_file": str(OUT_FILE),
    }
    STATS_FILE.write_text(json.dumps(stats, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"\n=== DONE ===")
    print(f"Total entries: {len(all_entries)}")
    print(f"By source: {by_tag}")
    print(f"Output: {OUT_FILE}")


if __name__ == "__main__":
    main()
