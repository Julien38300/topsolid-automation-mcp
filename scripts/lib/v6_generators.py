"""LoRA 3B v6 — conversational pattern generators.

Addresses LoRA v6 session MAJ findings (multi-turn tool_result + error
handling) plus Cortana-identified UX gaps (formatting, chaining, follow-up
disambiguation, write acks, graph Remarks relay).

All generators emit ShareGPT entries via make_multi_turn() helper defined
in the parent script. Recipe names are EN (MCP convention since v1.4.0).

~290 entries total across 7 categories (numbered 1a-1c, 2a-2c, 4-8).
"""
from __future__ import annotations

import random
import json


# ============================================================================
# Helpers (injected from parent script)
# ============================================================================
# The parent script provides make_multi_turn() and make_tool_call() — we
# receive them as function references to avoid circular imports.

# ============================================================================
# CATEGORY 1a — Multi-turn with useful data: extract, don't re-call (40 pairs)
# ============================================================================

MULTITURN_DATA_CASES = [
    {
        "q1": "c'est quoi la designation?",
        "recipe": "read_designation",
        "tool_result": '{"designation": "Bride support V2", "revision": "A"}',
        "q2": "et la revision?",
        "a2": "Revision A.",
    },
    {
        "q1": "masse de la piece",
        "recipe": "read_mass_volume",
        "tool_result": '{"mass_kg": 2.45, "volume_cm3": 312.5, "surface_m2": 0.084}',
        "q2": "et le volume?",
        "a2": "312.5 cm3.",
    },
    {
        "q1": "materiau?",
        "recipe": "read_material",
        "tool_result": '{"material": "Acier S235", "density_kg_m3": 7850}',
        "q2": "densite?",
        "a2": "7850 kg/m3.",
    },
    {
        "q1": "quelle est la reference?",
        "recipe": "read_reference",
        "tool_result": '{"reference": "REF-2026-001", "manufacturer": "Bosch"}',
        "q2": "fabricant?",
        "a2": "Bosch.",
    },
    {
        "q1": "read the designation",
        "recipe": "read_designation",
        "tool_result": '{"designation": "Motor support plate", "reference": "MSP-042"}',
        "q2": "what reference?",
        "a2": "MSP-042.",
    },
    {
        "q1": "combien de pieces dans l'assemblage?",
        "recipe": "count_assembly_parts",
        "tool_result": '{"total": 12, "by_type": {"TopPrt": 8, "TopFam": 4}}',
        "q2": "combien de pieces standard?",
        "a2": "8 pieces (TopPrt).",
    },
    {
        "q1": "dimensions du brut",
        "recipe": "read_bounding_box",
        "tool_result": '{"x_mm": 150.0, "y_mm": 80.0, "z_mm": 25.0}',
        "q2": "hauteur?",
        "a2": "25 mm.",
    },
    {
        "q1": "liste les parametres",
        "recipe": "read_parameters",
        "tool_result": '{"parameters": [{"name": "Longueur", "value": 0.150}, {"name": "Epaisseur", "value": 0.005}]}',
        "q2": "valeur de Longueur en mm?",
        "a2": "150 mm.",
    },
    {
        "q1": "proprietes pdm",
        "recipe": "read_pdm_properties",
        "tool_result": '{"name": "Bride-01", "designation": "Bride principale", "reference": "BR-001", "manufacturer": "Interne"}',
        "q2": "le nom?",
        "a2": "Bride-01.",
    },
    {
        "q1": "quel projet?",
        "recipe": "read_current_project",
        "tool_result": '{"project": "MachineOutilV3", "path": "D:\\\\Projects\\\\MachineOutilV3"}',
        "q2": "et le chemin complet?",
        "a2": "D:\\\\Projects\\\\MachineOutilV3.",
    },
    {
        "q1": "liste les shapes",
        "recipe": "read_shapes",
        "tool_result": '{"shapes": [{"name": "MainBody", "type": "Solid"}, {"name": "Ring1", "type": "Surface"}]}',
        "q2": "le type du premier?",
        "a2": "Solid.",
    },
    {
        "q1": "historique des revisions",
        "recipe": "read_revision_history",
        "tool_result": '{"revisions": [{"rev": "A", "date": "2026-01-15", "author": "Julien"}, {"rev": "B", "date": "2026-03-20", "author": "Anne"}]}',
        "q2": "qui a fait la B?",
        "a2": "Anne.",
    },
    {
        "q1": "inerties",
        "recipe": "read_inertia_moments",
        "tool_result": '{"Ixx": 0.0023, "Iyy": 0.0015, "Izz": 0.0041}',
        "q2": "le plus grand axe?",
        "a2": "Izz = 0.0041 kg.m2.",
    },
    {
        "q1": "liste les occurrences",
        "recipe": "read_occurrences",
        "tool_result": '{"occurrences": [{"name": "Axe-1", "definition": "Axe standard"}, {"name": "Bride-1", "definition": "Bride support"}, {"name": "Bride-2", "definition": "Bride support"}]}',
        "q2": "combien y en a-t-il?",
        "a2": "3 occurrences.",
    },
    {
        "q1": "echelle du plan",
        "recipe": "read_drafting_scale",
        "tool_result": '{"global_scale": "1:2", "views": [{"name": "Front", "scale": "1:1"}]}',
        "q2": "et l'echelle de la vue Front?",
        "a2": "1:1.",
    },
    {
        "q1": "audit piece",
        "recipe": "audit_part",
        "tool_result": '{"designation_set": true, "reference_set": false, "material_set": true, "mass_kg": 1.8}',
        "q2": "il manque la reference c'est ca?",
        "a2": "Oui, la reference n'est pas definie.",
    },
    {
        "q1": "cas d'emploi",
        "recipe": "read_where_used",
        "tool_result": '{"used_in": [{"doc": "Moteur.TopAsm", "count": 2}, {"doc": "BatiPrincipal.TopAsm", "count": 4}]}',
        "q2": "dans quel assemblage est-elle le plus utilisee?",
        "a2": "BatiPrincipal.TopAsm (4 fois).",
    },
    {
        "q1": "liste les inclusions",
        "recipe": "list_inclusions",
        "tool_result": '{"inclusions": [{"name": "Sous-Assy1", "parts": 5}, {"name": "Sous-Assy2", "parts": 3}]}',
        "q2": "combien de pieces dans Sous-Assy1?",
        "a2": "5.",
    },
    {
        "q1": "liste les documents du projet",
        "recipe": "list_project_documents",
        "tool_result": '{"documents": [{"name": "Bride", "type": "TopPrt", "ref": "BR-001"}, {"name": "Axe", "type": "TopPrt", "ref": ""}]}',
        "q2": "lesquels n'ont pas de reference?",
        "a2": "Axe.",
    },
    {
        "q1": "types de documents du projet",
        "recipe": "count_documents_by_type",
        "tool_result": '{"TopPrt": 24, "TopAsm": 5, "TopDft": 8}',
        "q2": "combien de mises en plan?",
        "a2": "8 (TopDft).",
    },
    {
        "q1": "liste les esquisses",
        "recipe": "list_sketches",
        "tool_result": '{"sketches": [{"name": "Esq-Base", "segments": 8}, {"name": "Esq-Trous", "segments": 4}]}',
        "q2": "combien de segments dans Esq-Base?",
        "a2": "8.",
    },
    {
        "q1": "liste les operations",
        "recipe": "read_operations",
        "tool_result": '{"operations": ["Extrude1", "Fillet1", "Hole1", "Pattern1"]}',
        "q2": "derniere operation?",
        "a2": "Pattern1.",
    },
    {
        "q1": "surface totale",
        "recipe": "read_total_surface",
        "tool_result": '{"surface_m2": 0.123, "surface_mm2": 123000}',
        "q2": "en mm2?",
        "a2": "123000 mm2.",
    },
    {
        "q1": "nomenclature",
        "recipe": "read_bom_contents",
        "tool_result": '{"rows": [{"id": 1, "ref": "A-001", "qty": 1}, {"id": 2, "ref": "A-002", "qty": 3}]}',
        "q2": "quantite de A-002?",
        "a2": "3.",
    },
    {
        "q1": "qui utilise ce document",
        "recipe": "read_where_used",
        "tool_result": '{"used_in": [{"doc": "AssyFinal", "count": 1}]}',
        "q2": "combien de fois?",
        "a2": "1 fois dans AssyFinal.",
    },
    {
        "q1": "couleurs de la piece",
        "recipe": "attr_read_color",
        "tool_result": '{"shapes": [{"name": "Main", "rgb": [128, 128, 128]}, {"name": "Insert", "rgb": [200, 50, 50]}]}',
        "q2": "le RGB de Insert?",
        "a2": "(200, 50, 50) — rouge.",
    },
    {
        "q1": "transparence",
        "recipe": "attr_read_transparency",
        "tool_result": '{"shapes": [{"name": "Body", "transparency": 0.0}, {"name": "Glass", "transparency": 0.8}]}',
        "q2": "Glass transparent a combien?",
        "a2": "80% (0.8).",
    },
    {
        "q1": "liste les layers",
        "recipe": "attr_list_layers",
        "tool_result": '{"layers": ["Default", "Construction", "Hidden", "Annotation"]}',
        "q2": "combien y en a-t-il?",
        "a2": "4 layers.",
    },
    {
        "q1": "verifie les drivers famille",
        "recipe": "check_family_drivers",
        "tool_result": '{"ok": 8, "missing_designation": ["DriverX", "DriverY"]}',
        "q2": "combien manquent-il?",
        "a2": "2 (DriverX et DriverY).",
    },
    {
        "q1": "densite",
        "recipe": "read_material_density",
        "tool_result": '{"density_kg_m3": 7850, "mass_kg": 2.1, "volume_cm3": 267.5}',
        "q2": "masse?",
        "a2": "2.1 kg.",
    },
    {
        "q1": "documents modifies non sauves",
        "recipe": "find_modified_documents",
        "tool_result": '{"modified": [{"name": "Bride", "dirty": true}, {"name": "Axe", "dirty": true}]}',
        "q2": "combien en tout?",
        "a2": "2 documents modifies.",
    },
    {
        "q1": "donne moi tous les parametres",
        "recipe": "read_parameters",
        "tool_result": '{"parameters": [{"name": "Longueur", "value": 0.150, "unit": "m"}, {"name": "Angle", "value": 0.785, "unit": "rad"}]}',
        "q2": "Angle en degres?",
        "a2": "45 degres (0.785 rad).",
    },
    {
        "q1": "liste des points 3d",
        "recipe": "read_3d_points",
        "tool_result": '{"points": [{"name": "P1", "x": 0.05, "y": 0.0, "z": 0.0}, {"name": "P2", "x": 0.1, "y": 0.05, "z": 0.0}]}',
        "q2": "X de P2 en mm?",
        "a2": "100 mm.",
    },
    {
        "q1": "nom du document",
        "recipe": "read_name",
        "tool_result": '{"name": "Bride-01.TopPrt", "type": "Part"}',
        "q2": "c'est quoi comme type?",
        "a2": "Part.",
    },
    {
        "q1": "chercher document Bride",
        "recipe": "search_document",
        "tool_result": '{"matches": ["Bride-01", "Bride-Support", "Bride-Principale"]}',
        "q2": "le premier?",
        "a2": "Bride-01.",
    },
    {
        "q1": "audit references manquantes",
        "recipe": "list_documents_without_reference",
        "tool_result": '{"documents": ["Axe", "Entretoise", "Vis-M4"]}',
        "q2": "combien?",
        "a2": "3 documents sans reference.",
    },
    {
        "q1": "format du plan",
        "recipe": "read_drafting_format",
        "tool_result": '{"format": "A3", "width_mm": 420, "height_mm": 297}',
        "q2": "largeur?",
        "a2": "420 mm.",
    },
    {
        "q1": "cartouche",
        "recipe": "read_title_block",
        "tool_result": '{"title": "Bride support", "author": "Julien", "date": "2026-04-19", "scale": "1:2"}',
        "q2": "qui a fait ce plan?",
        "a2": "Julien.",
    },
    {
        "q1": "liste les vues",
        "recipe": "list_drafting_views",
        "tool_result": '{"views": [{"name": "Front", "type": "Projection"}, {"name": "Coupe-AA", "type": "Section"}]}',
        "q2": "y a-t-il une coupe?",
        "a2": "Oui, Coupe-AA.",
    },
]


def gen_multiturn_data(make_multi_turn, make_tool_call):
    """1a) Multi-turn with useful data — model extracts from previous result."""
    entries = []
    for c in MULTITURN_DATA_CASES:
        entries.append(make_multi_turn([
            ("user", c["q1"]),
            ("assistant", make_tool_call(c["recipe"])),
            ("tool", c["tool_result"]),
            ("user", c["q2"]),
            ("assistant", c["a2"]),
        ]))
    return entries


# ============================================================================
# CATEGORY 1b — Empty/null result: interpret as text (30 pairs)
# ============================================================================

EMPTY_RESULT_CASES = [
    ("materiau defini?", "read_material", '{"material": null, "density_kg_m3": null}', "Alors?", "Pas de materiau defini sur la piece."),
    ("liste parametres", "read_parameters", '{"parameters": []}', "rien?", "Aucun parametre defini."),
    ("occurrences", "read_occurrences", '{"occurrences": []}', "ok?", "Pas d'occurrences — le document n'est pas un assemblage."),
    ("esquisses", "list_sketches", '{"sketches": []}', "vraiment?", "Aucune esquisse dans ce document."),
    ("points 3d", "read_3d_points", '{"points": []}', "et alors?", "Aucun point 3D defini."),
    ("designation?", "read_designation", '{"designation": ""}', "hmm?", "La designation est vide."),
    ("reference?", "read_reference", '{"reference": null}', "??", "Pas de reference definie."),
    ("fabricant?", "read_manufacturer", '{"manufacturer": ""}', "rien?", "Aucun fabricant renseigne."),
    ("inclusions", "list_inclusions", '{"inclusions": []}', "?", "Pas d'inclusions — document autonome."),
    ("drivers famille", "read_family_drivers", '{"drivers": []}', "?", "Aucun driver de famille — ce n'est pas un document famille."),
    ("revisions", "read_revision_history", '{"revisions": []}', "??", "Pas d'historique de revisions."),
    ("couleurs faces", "attr_read_face_colors", '{"faces": []}', "?", "Pas de faces colorees."),
    ("where used?", "read_where_used", '{"used_in": []}', "alors?", "Ce document n'est utilise dans aucun assemblage."),
    ("documents projet", "list_project_documents", '{"documents": []}', "bizarre?", "Le projet est vide (aucun document)."),
    ("plis tole", "read_bend_features", '{"bends": []}', "??", "Pas de plis — ce n'est pas une tole depliee."),
    ("vues du plan", "list_drafting_views", '{"views": []}', "?", "Aucune vue definie dans ce plan."),
    ("lignes BOM", "count_bom_rows", '{"count": 0}', "?", "La nomenclature est vide."),
    ("operations", "read_operations", '{"operations": []}', "?", "Aucune operation dans l'arbre."),
    ("shapes", "read_shapes", '{"shapes": []}', "?", "Aucune shape dans ce document."),
    ("Longueur", "read_real_parameter", '{"value": null}', "?", "Le parametre Longueur n'existe pas."),
    ("layers", "attr_list_layers", '{"layers": []}', "??", "Aucun calque defini."),
    ("docs modifies", "find_modified_documents", '{"modified": []}', "?", "Tous les documents du projet sont sauvegardes."),
    ("dossier cherche", "search_folder", '{"matches": []}', "?", "Aucun dossier trouve avec ce nom."),
    ("materiaux manquants", "check_missing_materials", '{"missing": []}', "!", "Tous les documents du projet ont un materiau defini."),
    ("audit piece", "audit_part", '{"ok": true, "issues": []}', "?", "La piece est complete (designation, reference, materiau tous definis)."),
    ("instances famille", "read_family_instances", '{"instances": []}', "?", "Pas d'instances — ce n'est pas une famille."),
    ("contraintes esquisse", "read_sketch_constraints", '{"constraints": []}', "?", "Cette esquisse n'a pas de contraintes."),
    ("documents sans designation", "list_documents_without_designation", '{"documents": []}', "?", "Tous les documents du projet ont une designation."),
    ("segments esquisse", "read_sketch_segments", '{"segments": []}', "?", "Aucun segment dans cette esquisse."),
    ("cotes du plan", "read_drafting_dimensions", '{"dimensions": []}', "?", "Aucune cote sur ce plan."),
]


def gen_empty_results(make_multi_turn, make_tool_call):
    """1b) Null/empty results — interpret as text, NOT re-call tool."""
    entries = []
    for q1, recipe, tool_result, q2, a2 in EMPTY_RESULT_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", make_tool_call(recipe)),
            ("tool", tool_result),
            ("user", q2),
            ("assistant", a2),
        ]))
    return entries


# ============================================================================
# CATEGORY 1c — Acknowledgments after response (10 pairs)
# ============================================================================

ACK_CASES = [
    ("projet courant?", "read_current_project", '{"project": "Test01"}', "ok", "Ok."),
    ("designation?", "read_designation", '{"designation": "Bride-01"}', "merci", "De rien."),
    ("masse?", "read_mass_volume", '{"mass_kg": 2.4}', "compris", "Bien recu."),
    ("reference?", "read_reference", '{"reference": "R-001"}', "nickel", "Parfait."),
    ("materiau?", "read_material", '{"material": "Aluminium"}', "cool", "Avec plaisir."),
    ("combien de pieces?", "count_assembly_parts", '{"total": 10}', "ok tres bien", "Content que ca aide."),
    ("fabricant?", "read_manufacturer", '{"manufacturer": "SKF"}', "thanks", "You're welcome."),
    ("version?", "read_document_state", '{"version": "A.2"}', "parfait", "De rien."),
    ("type doc?", "document_type", '{"type": "TopPrt"}', "ok c'est note", "Ok."),
    ("nom?", "read_name", '{"name": "Axe-01"}', "ok ca me va", "Ok."),
]


def gen_acks(make_multi_turn, make_tool_call):
    """1c) Simple acks (ok, merci, compris) after response — NO tool call."""
    entries = []
    for q1, recipe, tool_result, q2, a2 in ACK_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", make_tool_call(recipe)),
            ("tool", tool_result),
            ("user", q2),
            ("assistant", a2),
        ]))
    return entries


# ============================================================================
# CATEGORY 2a — Error then "retry" (10 pairs)
# ============================================================================

RETRY_CASES = [
    ("etat de TopSolid?", "get_state_call", '{"error": "TopSolid non connecte"}', "ok c'est fait, reessaye", "get_state_call"),
    ("designation?", "read_designation", '{"error": "TopSolid non connecte"}', "relance TopSolid, c'est ok", "read_designation"),
    ("masse?", "read_mass_volume", '{"error": "Connexion Automation perdue"}', "j'ai relance, vas-y", "read_mass_volume"),
    ("liste params", "read_parameters", '{"error": "TopSolid n\'est pas lance"}', "ok c'est lance maintenant", "read_parameters"),
    ("export step", "export_step", '{"error": "Impossible de se connecter sur 8090"}', "port ouvert, ressaie", "export_step"),
    ("materiau?", "read_material", '{"error": "Connexion timeout"}', "retente", "read_material"),
    ("check part", "check_part", '{"error": "TopSolid n\'est pas demarre"}', "fait", "check_part"),
    ("current project", "read_current_project", '{"error": "No connection"}', "retry", "read_current_project"),
    ("save document", "save_document", '{"error": "Not connected to TopSolid"}', "reconnecte, go", "save_document"),
    ("count parts", "count_assembly_parts", '{"error": "Automation not available"}', "pret, refais", "count_assembly_parts"),
]


def gen_retries(make_multi_turn, make_tool_call):
    """2a) Error 'not connected' + user 'retry' -> re-call SAME tool."""
    entries = []
    for q1, recipe1, err, q2, recipe2 in RETRY_CASES:
        # For get_state_call, use the actual tool name
        if recipe1 == "get_state_call":
            tc1 = '[TOOL_CALLS]topsolid__topsolid_get_state[ARGS]{}'
            tc2 = '[TOOL_CALLS]topsolid__topsolid_get_state[ARGS]{}'
        else:
            tc1 = make_tool_call(recipe1)
            tc2 = make_tool_call(recipe2)
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", tc1),
            ("tool", err),
            ("user", q2),
            ("assistant", tc2),
        ]))
    return entries


# ============================================================================
# CATEGORY 2b — Error "not found" -> propose search (15 pairs)
# ============================================================================

NOT_FOUND_CASES = [
    ("ouvre Bride V3", "open_document_by_name", "Bride V3", '{"error": "Document non trouve"}', "Ah mince", "search_document", "Bride"),
    ("ouvre Axe principal", "open_document_by_name", "Axe principal", '{"error": "Document \'Axe principal\' non trouve"}', "flute", "search_document", "Axe"),
    ("cherche dossier Assembly", "search_folder", "Assembly", '{"error": "Dossier non trouve"}', "zut", "search_folder", "Assy"),
    ("open doc MotorMount", "open_document_by_name", "MotorMount", '{"error": "Not found"}', "damn", "search_document", "Motor"),
    ("ouvre Plaque-42", "open_document_by_name", "Plaque-42", '{"error": "Document non trouve dans le projet"}', "??", "search_document", "Plaque"),
    ("ouvre Ensemble Alpha", "open_document_by_name", "Ensemble Alpha", '{"error": "Not found"}', "merde", "search_document", "Ensemble"),
    ("dossier Outils", "search_folder", "Outils", '{"error": "No such folder"}', "oh", "search_folder", "Outil"),
    ("ouvre Bridage Principal", "open_document_by_name", "Bridage Principal", '{"error": "Document introuvable"}', "ah non", "search_document", "Bridage"),
    ("open Gearbox-v2", "open_document_by_name", "Gearbox-v2", '{"error": "Document \'Gearbox-v2\' not found"}', "oops", "search_document", "Gearbox"),
    ("ouvre Support moteur V5", "open_document_by_name", "Support moteur V5", '{"error": "Not found"}', "fait chier", "search_document", "Support moteur"),
    ("cherche dossier Export", "search_folder", "Export", '{"error": "Pas de dossier nomme Export"}', "bon", "search_folder", "Exports"),
    ("ouvre Vis M8x40", "open_document_by_name", "Vis M8x40", '{"error": "Document not found"}', "???", "search_document", "Vis M8"),
    ("open BearingHouse", "open_document_by_name", "BearingHouse", '{"error": "Introuvable"}', "strange", "search_document", "Bearing"),
    ("ouvre Chassis Final", "open_document_by_name", "Chassis Final", '{"error": "Document \'Chassis Final\' not found"}', "m.rde", "search_document", "Chassis"),
    ("cherche doss Draft", "search_folder", "Draft", '{"error": "Pas trouve"}', "oui", "search_folder", "Drafting"),
]


def gen_not_found(make_multi_turn, make_tool_call):
    """2b) 'Not found' error -> propose search with partial name."""
    entries = []
    for q1, recipe1, val1, err, q2, recipe2, val2 in NOT_FOUND_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", make_tool_call(recipe1, value=val1)),
            ("tool", err),
            ("user", q2),
            ("assistant", make_tool_call(recipe2, value=val2)),
        ]))
    return entries


# ============================================================================
# CATEGORY 2c — Structural error -> relay as text (15 pairs)
# ============================================================================

STRUCT_ERROR_CASES = [
    ("combien de pieces?", "count_assembly_parts", '{"error": "Le document courant n\'est pas un assemblage"}', "ah bon?", "Le document courant n'est pas un assemblage."),
    ("liste inclusions", "list_inclusions", '{"error": "Document is not an assembly"}', "vraiment?", "Oui, le document n'est pas un assemblage."),
    ("cote du plan", "read_drafting_dimensions", '{"error": "Not a drafting document"}', "sérieux?", "Le document courant n'est pas une mise en plan."),
    ("rapport masse assemblage", "assembly_mass_report", '{"error": "Not an assembly"}', "bon", "Le document courant n'est pas un assemblage."),
    ("depliage dims", "read_unfolding_dimensions", '{"error": "Document is not an unfolding"}', "?", "Le document n'est pas une mise a plat."),
    ("list drafting views", "list_drafting_views", '{"error": "Not a TopDft"}', "??", "Le document n'est pas une mise en plan."),
    ("detect family drivers", "read_family_drivers", '{"error": "Not a family document"}', "?", "Le document n'est pas une famille."),
    ("check plis", "read_bend_features", '{"error": "Document is not an unfolding"}', "!", "Le document n'est pas une mise a plat."),
    ("bom contents", "read_bom_contents", '{"error": "Not a BOM document"}', "oh", "Le document n'est pas une nomenclature."),
    ("check assembly", "audit_assembly", '{"error": "Not an assembly"}', "ah", "Le document n'est pas un assemblage."),
    ("read parameters", "read_parameters", '{"error": "No document edited"}', "zut", "Aucun document n'est actuellement en edition."),
    ("save document", "save_document", '{"error": "Document is read-only"}', "??", "Le document est en lecture seule."),
    ("set designation", "set_designation", '{"error": "Document locked by another user"}', "bon ok", "Le document est verrouille par un autre utilisateur."),
    ("rebuild document", "rebuild_document", '{"error": "Document has compilation errors"}', "oh non", "Le document a des erreurs de compilation et ne peut pas etre reconstruit."),
    ("export step", "export_step", '{"error": "No write permission on target folder"}', "bon", "Pas de droits d'ecriture sur le dossier cible."),
]


def gen_structural_errors(make_multi_turn, make_tool_call):
    """2c) Structural error -> relay as text. NEVER invent a number."""
    entries = []
    for q1, recipe, err, q2, a2 in STRUCT_ERROR_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", make_tool_call(recipe)),
            ("tool", err),
            ("user", q2),
            ("assistant", a2),
        ]))
    return entries


# ============================================================================
# CATEGORY 4 — Formatted return value (30 pairs)
# ============================================================================

FORMATTED_RETURN_CASES = [
    ("designation?", "read_designation", '"Designation: Bride support V2"', "La designation est **Bride support V2**."),
    ("reference?", "read_reference", '"Reference: REF-2026-001"', "La reference est **REF-2026-001**."),
    ("fabricant?", "read_manufacturer", '"Manufacturer: Bosch"', "Fabricant : **Bosch**."),
    ("nom?", "read_name", '"Name: Bride-01"', "Nom du document : **Bride-01**."),
    ("projet?", "read_current_project", '"Project: MachineOutilV3"', "Projet courant : **MachineOutilV3**."),
    ("masse et volume", "read_mass_volume", '"Mass: 2.450 kg, Volume: 312.5 cm3, Surface: 0.084 m2"', "**Masse** : 2.45 kg, **volume** : 312.5 cm3, **surface** : 0.084 m2."),
    ("version?", "read_revision", '"Revision: B.2"', "Revision **B.2**."),
    ("type doc?", "document_type", '"Type: TopPrt (Part)"', "C'est une **piece** (TopPrt)."),
    ("combien pieces?", "count_assembly_parts", '"Total: 12 parts (8 standard + 4 famille)"', "**12 pieces** au total (8 standard, 4 famille)."),
    ("dimensions?", "read_bounding_box", '"Width: 150 mm, Height: 25 mm, Length: 80 mm"', "Dimensions : **150 × 80 × 25 mm**."),
    ("echelle plan", "read_drafting_scale", '"Global scale: 1:2"', "Echelle globale : **1:2**."),
    ("format plan", "read_drafting_format", '"Format: A3 (420 × 297 mm)"', "Format **A3** (420 × 297 mm)."),
    ("surface totale", "read_total_surface", '"Total surface: 0.123 m2 (123 000 mm2)"', "Surface totale : **0.123 m2** (123 000 mm2)."),
    ("densite?", "read_material_density", '"Density: 7850 kg/m3"', "Densite : **7850 kg/m3** (acier typique)."),
    ("materiau?", "read_material", '"Material: Acier S235"', "Materiau : **Acier S235**."),
    ("proprietes pdm", "read_pdm_properties", '"Name: Bride-01, Designation: Bride support, Reference: BR-001, Manufacturer: Interne"', "**Bride-01** — Designation *Bride support*, Reference *BR-001*, Fabricant *Interne*."),
    ("list layers", "attr_list_layers", '"Layers: Default, Construction, Hidden, Annotation"', "4 calques : **Default**, **Construction**, **Hidden**, **Annotation**."),
    ("revision history", "read_revision_history", '"A (2026-01-15 Julien), B (2026-03-20 Anne)"', "2 revisions : **A** (15/01 par Julien) et **B** (20/03 par Anne)."),
    ("cas emploi", "read_where_used", '"Used in: Moteur.TopAsm (x2), BatiPrincipal.TopAsm (x4)"', "Utilise dans **Moteur** (2x) et **BatiPrincipal** (4x)."),
    ("cartouche", "read_title_block", '"Title: Bride support, Author: Julien, Date: 2026-04-19, Scale: 1:2"', "Cartouche : *Bride support*, par **Julien** le 19/04/2026, echelle **1:2**."),
    ("drivers famille", "check_family_drivers", '"OK: 8, Missing designation: DriverX, DriverY"', "**8 drivers OK**, **2 manquent** (DriverX, DriverY)."),
    ("points 3d", "read_3d_points", '"Points: P1 (50, 0, 0) mm, P2 (100, 50, 0) mm"', "2 points : **P1** (50, 0, 0) mm et **P2** (100, 50, 0) mm."),
    ("audit piece", "audit_part", '"OK: designation, reference, material set. Mass: 1.8 kg"', "Piece complete (designation, reference, materiau). **Masse : 1.8 kg**."),
    ("inerties", "read_inertia_moments", '"Ixx: 0.0023, Iyy: 0.0015, Izz: 0.0041 kg.m2"', "Inerties principales : **Ixx=0.0023**, **Iyy=0.0015**, **Izz=0.0041** kg.m2."),
    ("param Longueur", "read_real_parameter", '"Longueur: 0.150 m (150 mm)"', "Longueur : **150 mm** (0.150 m)."),
    ("doc modifies", "find_modified_documents", '"Modified: Bride-01, Axe-04"', "2 documents modifies : **Bride-01**, **Axe-04**."),
    ("docs projet", "count_documents_by_type", '"TopPrt: 24, TopAsm: 5, TopDft: 8"', "Projet : **24 pieces**, **5 assemblages**, **8 plans**."),
    ("occurrences", "read_occurrences", '"Occurrences: Axe-1 (Axe std), Bride-1 (Bride sup), Bride-2 (Bride sup)"', "3 occurrences : **Axe-1**, **Bride-1**, **Bride-2**."),
    ("inclusions", "list_inclusions", '"Inclusions: Sous-Assy1 (5 parts), Sous-Assy2 (3 parts)"', "2 sous-assemblages : **Sous-Assy1** (5p) et **Sous-Assy2** (3p)."),
    ("transparence", "attr_read_transparency", '"Body: 0.0, Glass: 0.8"', "**Body** opaque, **Glass** transparent a **80%**."),
]


def gen_formatted_returns(make_multi_turn, make_tool_call):
    """4) MCP returns a string, model re-formats in nice prose."""
    entries = []
    for q, recipe, tool_result, assistant in FORMATTED_RETURN_CASES:
        entries.append(make_multi_turn([
            ("user", q),
            ("assistant", make_tool_call(recipe)),
            ("tool", tool_result),
            ("assistant", assistant),  # auto-continue after tool result, no new user turn
        ]))
    return entries


# ============================================================================
# CATEGORY 5 — Recipe chaining (40 pairs)
# ============================================================================

CHAIN_CASES = [
    # Sequential: recipe1 result feeds into recipe2
    ("masse et materiau", "read_mass_volume", '{"mass_kg": 2.45}', "et le materiau?", "read_material"),
    ("nom puis designation", "read_name", '{"name": "Bride"}', "et la designation?", "read_designation"),
    ("reference puis fabricant", "read_reference", '{"reference": "R-001"}', "fabricant maintenant", "read_manufacturer"),
    ("bounding box puis surface", "read_bounding_box", '{"x": 150, "y": 80, "z": 25}', "surface totale?", "read_total_surface"),
    ("params puis longueur", "read_parameters", '{"parameters": ["L", "H"]}', "donne L en detail", "read_real_parameter"),
    ("docs projet puis sans ref", "list_project_documents", '{"total": 30}', "lesquels sans reference?", "list_documents_without_reference"),
    ("count parts puis details", "count_assembly_parts", '{"total": 12}', "liste les occurrences", "read_occurrences"),
    ("audit piece puis materiaux", "audit_part", '{"issues": []}', "verifie les materiaux manquants dans le projet", "check_missing_materials"),
    ("detecter asm puis liste", "detect_assembly", '{"is_assembly": true}', "liste les inclusions", "list_inclusions"),
    ("projet puis documents", "read_current_project", '{"project": "Test"}', "liste les documents", "list_project_documents"),
    ("revision puis history", "read_revision", '{"revision": "B"}', "montre tout l'historique", "read_revision_history"),
    ("type doc puis famille?", "document_type", '{"type": "TopFam"}', "drivers alors?", "read_family_drivers"),
    ("shapes puis couleurs", "read_shapes", '{"count": 3}', "et les couleurs?", "attr_read_color"),
    ("shapes puis transparence", "read_shapes", '{"count": 2}', "transparence de chaque?", "attr_read_transparency"),
    ("layers puis reaffecter", "attr_list_layers", '{"layers": ["L1", "L2"]}', "mets la shape sur L2", "attr_assign_layer"),
    ("audit projet puis fix", "check_project", '{"missing_ref": 3}', "liste ceux sans designation", "list_documents_without_designation"),
    ("format plan puis vues", "read_drafting_format", '{"format": "A3"}', "liste les vues", "list_drafting_views"),
    ("echelle puis projection", "read_drafting_scale", '{"scale": "1:2"}', "projection principale?", "read_main_projection"),
    ("bom count puis content", "count_bom_rows", '{"count": 25}', "donne le contenu", "read_bom_contents"),
    ("cas emploi puis pieces", "read_where_used", '{"count": 2}', "compte les pieces du parent", "count_assembly_parts"),
    # "Both X and Y" → chain 2 reads
    ("donne masse ET volume", "read_mass_volume", '{"mass_kg": 2, "volume_cm3": 100}', "et la densite?", "read_material_density"),
    ("nom ET reference du doc", "read_name", '{"name": "Bride"}', "et ref?", "read_reference"),
    ("designation ET fabricant", "read_designation", '{"designation": "Bride"}', "fabricant", "read_manufacturer"),
    ("echelle ET format", "read_drafting_scale", '{"scale": "1:1"}', "format?", "read_drafting_format"),
    ("params ET occurrences", "read_parameters", '{"params": []}', "occurrences", "read_occurrences"),
    # "After X, do Y"
    ("apres lecture params, liste operations", "read_parameters", '{}', "operations maintenant", "read_operations"),
    ("lis designation puis sauve", "read_designation", '"D: Bride"', "sauve le document", "save_document"),
    ("verifie famille puis instances", "detect_family", '{"is_family": true}', "liste les instances", "read_family_instances"),
    ("audit puis reconstruit", "audit_part", '{"ok": true}', "reconstruis", "rebuild_document"),
    ("check drivers puis fix", "check_family_drivers", '{"missing": 2}', "corrige les drivers manquants", "fix_family_drivers"),
    # Comparative chains
    ("params document actuel", "read_parameters", '{"L": 150}', "compare avec Bride-02", "compare_parameters"),
    ("operations actuelles", "read_operations", '{"ops": 5}', "compare operations avec Ref", "compare_document_operations"),
    ("entites", "read_shapes", '{"n": 3}', "compare entities avec Model-v2", "compare_document_entities"),
    ("revision", "read_revision", '{"rev": "B"}', "compare avec rev precedente", "compare_revisions"),
    # Batch after single
    ("ma piece ok?", "audit_part", '{"ok": true}', "audite tout le projet", "batch_audit_project"),
    ("exporte cette piece en step", "export_step", '{}', "exporte tout le projet", "batch_export_step"),
    ("mon doc est dirty?", "detect_unfolding", '{"dirty": true}', "verifie tous les virtuels", "batch_check_virtual"),
    ("param noms audit", "audit_parameter_names", '{}', "audite le projet entier", "batch_audit_parameter_names"),
    ("efface auteur ici", "clear_document_author", '{"ok": true}', "fais-le sur tout le projet", "batch_clear_author"),
    ("lis une propriete", "read_user_property", '{"val": null}', "lis cette propriete sur tout le projet", "batch_read_property"),
]


def gen_chains(make_multi_turn, make_tool_call):
    """5) Sequential recipe chaining."""
    entries = []
    for q1, recipe1, result, q2, recipe2 in CHAIN_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", make_tool_call(recipe1)),
            ("tool", result),
            ("user", q2),
            ("assistant", make_tool_call(recipe2)),
        ]))
    return entries


# ============================================================================
# CATEGORY 6 — Follow-up disambiguation (20 pairs)
# ============================================================================

FOLLOWUP_CASES = [
    # Ambiguous first → ask → user completes → tool call
    ("parametre longueur", "Lire ou modifier la valeur du parametre Longueur ?", "lire", "read_real_parameter", None),
    ("parametre Epaisseur", "Lire ou modifier Epaisseur ? Et quelle nouvelle valeur si modification ?", "modifier a 5mm", "set_real_parameter", "Epaisseur:5mm"),
    ("change la couleur", "Quelle couleur ? Donnez-moi une valeur RGB (par ex. 255,0,0 pour rouge).", "en rouge", "attr_set_color", "255,0,0"),
    ("mets une couleur", "Quelle couleur exactement ?", "bleu", "attr_set_color", "0,0,255"),
    ("exporte le doc", "En quel format ? STEP, IGES, STL, PDF, DXF ou 3D PDF ?", "step", "export_step", None),
    ("fais un export", "Quel format ? STEP, STL, PDF, DXF ?", "pdf", "export_pdf", None),
    ("renomme la piece", "Quel nouveau nom ?", "PIECE-001", "set_name", "PIECE-001"),
    ("change la designation", "Quelle nouvelle designation ?", "Bride support V3", "set_designation", "Bride support V3"),
    ("mets a jour", "Mettre a jour quoi exactement ? designation, reference, materiau, reconstruction ?", "reconstruit", "rebuild_document", None),
    ("modifie le parametre", "Quel parametre et quelle valeur ?", "Hauteur a 100mm", "set_real_parameter", "Hauteur:100mm"),
    ("change", "Changer quoi ? designation, reference, materiau, couleur... ?", "la reference en REF-999", "set_reference", "REF-999"),
    ("compare", "Comparer quoi ? revisions, parametres, operations, ou deux documents ?", "revisions", "compare_revisions", None),
    ("audit", "Auditer quoi ? la piece courante, un assemblage, ou tout le projet ?", "le projet", "batch_audit_project", None),
    ("transparence", "Lire ou modifier la transparence ?", "modifier a 50%", "attr_set_transparency", "0.5"),
    ("mets un materiau", "Quel materiau ?", "Acier S235", "set_material", "Acier S235"),
    ("ouvre un doc", "Quel document ?", "Bride-01", "open_document_by_name", "Bride-01"),
    ("cherche", "Chercher un document ou un dossier ? Avec quel mot-cle ?", "doc Bride", "search_document", "Bride"),
    ("set parameter", "Which parameter and what value ?", "Length to 200mm", "set_real_parameter", "Length:200mm"),
    ("change a layer", "Which layer ? New name or move an element to which layer ?", "move Shape1 to L1", "attr_assign_layer", "Shape1:L1"),
    ("audit family", "Audit which family ? Current document or all project families ?", "tout", "batch_audit_driver_designations", None),
]


def gen_followup(make_multi_turn, make_tool_call):
    """6) Ambiguous query -> ask for clarification -> user completes -> tool call."""
    entries = []
    for q1, clarification, q2, recipe, value in FOLLOWUP_CASES:
        entries.append(make_multi_turn([
            ("user", q1),
            ("assistant", clarification),
            ("user", q2),
            ("assistant", make_tool_call(recipe, value=value)),
        ]))
    return entries


# ============================================================================
# CATEGORY 7 — Acknowledgment after write (20 pairs)
# ============================================================================

WRITE_ACK_CASES = [
    ("mets la designation a Bride V2", "set_designation", "Bride V2", '"OK: Designation -> Bride V2"', "C'est fait, la designation est maintenant **Bride V2**."),
    ("change la reference en REF-100", "set_reference", "REF-100", '"OK: Reference -> REF-100"', "Reference mise a jour : **REF-100**."),
    ("fabricant Bosch", "set_manufacturer", "Bosch", '"OK: Manufacturer -> Bosch"', "Fabricant enregistre : **Bosch**."),
    ("renomme en Axe-Final", "set_name", "Axe-Final", '"OK: Name -> Axe-Final"', "Document renomme : **Axe-Final**."),
    ("peins tout en rouge", "attr_set_color_all", "255,0,0", '"OK: 5 shapes set to red"', "Couleur rouge appliquee a **5 shapes**."),
    ("mets la shape en bleu", "attr_set_color", "0,0,255", '"OK: 1 shape set to blue"', "Shape en bleu."),
    ("transparence 50%", "attr_set_transparency", "0.5", '"OK: transparency set to 0.5 for 3 shapes"', "Transparence a **50%** sur 3 shapes."),
    ("sauvegarde", "save_document", None, '"OK: document saved"', "Document sauvegarde."),
    ("sauve tout le projet", "save_all_project", None, '"OK: 24 documents saved, 0 errors"', "**24 documents sauvegardes** sans erreur."),
    ("reconstruis", "rebuild_document", None, '"OK: rebuild successful"', "Reconstruction terminee avec succes."),
    ("mets longueur a 150mm", "set_real_parameter", "Longueur:150mm", '"OK: Longueur = 0.150 m (150 mm)"', "Longueur reglee a **150 mm**."),
    ("angle 45 degres", "set_real_parameter", "Angle:45deg", '"OK: Angle = 0.7854 rad (45 deg)"', "Angle reglé à **45 degres** (0.7854 rad)."),
    ("efface l'auteur", "clear_document_author", None, '"OK: author cleared"', "Champ auteur vide."),
    ("vide auteur sur le projet", "batch_clear_author", None, '"OK: 18/18 documents cleared"', "Auteur efface sur **18 documents**."),
    ("active mode virtuel", "enable_virtual_document", None, '"OK: document is now virtual"', "Document passe en mode virtuel."),
    ("active virtuel sur tout", "batch_enable_virtual", None, '"OK: 12 documents switched to virtual"', "**12 documents** passes en mode virtuel."),
    ("change material Aluminium", "set_material", "Aluminium 6061", '"OK: material -> Aluminium 6061"', "Materiau change pour **Aluminium 6061**."),
    ("renomme occurrence Vis en Boulon", "rename_occurrence", "Vis:Boulon", '"OK: Vis -> Boulon"', "Occurrence renommee **Boulon**."),
    ("corrige les drivers manquants", "fix_family_drivers", None, '"OK: 3 drivers designations fixed"', "**3 drivers** corriges."),
    ("exporte en STEP", "export_step", "C:/temp/piece.stp", '"OK: exported to C:/temp/piece.stp"', "Exporte en STEP vers **C:/temp/piece.stp**."),
]


def gen_write_acks(make_multi_turn, make_tool_call):
    """7) After successful write, confirm in natural language."""
    entries = []
    for q, recipe, value, tool_result, assistant in WRITE_ACK_CASES:
        entries.append(make_multi_turn([
            ("user", q),
            ("assistant", make_tool_call(recipe, value=value)),
            ("tool", tool_result),
            ("assistant", assistant),
        ]))
    return entries


# ============================================================================
# CATEGORY 8 — Relay Remarks from graph (50 pairs)
# ============================================================================

REMARKS_CASES = [
    # Deprecation warnings
    ("cree une cote lineaire entre 2 points", "create_linear_dim_warn", "Cette methode est **depreciee** depuis v7.20.255. Utilisez plutot l'interface IDimensions. Veux-tu que je l'utilise ?"),
    ("copie plusieurs objets", "copy_several_warn", "La recette `copy_several_legacy` est **depreciee** (v7.20.258) — utilise `copy_several_with_options` a la place."),
    # Transactional warnings
    ("change la designation", "set_designation_warn", "Note : cette operation necessite un StartModification et va marquer le document comme dirty. Ok pour lancer ?"),
    ("mets a jour la reference", "set_reference_warn", "Cette ecriture est transactionnelle (Pattern D). Le docId peut changer apres l'execution. Tu veux quand meme ?"),
    # Document type constraints
    ("liste les pieces", "assembly_only", "Cette operation ne fonctionne que sur un **assemblage** (TopAsm). Verifie que le document courant est bien un assemblage."),
    ("plis de la tole", "unfolding_only", "Cette operation necessite un document **mise a plat** (TopUnf). Si ce n'est pas le cas, ca echouera."),
    ("vues du plan", "drafting_only", "Il faut un document **mise en plan** (TopDft). Je lance quand meme ?"),
    # SI units warnings
    ("longueur a 50", "si_units_warn", "TopSolid utilise le **metre** comme unite SI. 50 sera interprete comme 50 metres. Tu voulais 50 millimetres ?"),
    ("angle a 45", "si_deg_warn", "En SI, les angles sont en **radians**. 45 = 2578 degres ! Tu voulais 45 degres (= 0.7854 rad) ?"),
    # Documentation relay
    ("comment fonctionne EnsureIsDirty", "doc_relay_1", "`EnsureIsDirty(ref docId)` rend le document modifiable et **peut changer le docId**. Toujours reprendre les IDs dependants APRES cet appel."),
    ("docs de StartModification", "doc_relay_2", "`StartModification(label, unused)` ouvre une transaction. Il faut imperativement fermer avec `EndModification(commit=true/false, updateDisplay=true/false)`."),
    ("a quoi sert Pdm.Save", "doc_relay_3", "`Pdm.Save(pdmObjectId, saveReferences)` persiste le document sur disque. Toujours appeler apres un EndModification(true, true)."),
    # Since version
    ("get_face_cone_length exist?", "since_721", "Oui, mais cette methode est **disponible depuis TopSolid 7.21**. Verifie ta version si ca echoue."),
    ("read_item_last_operation", "since_721_2", "Disponible **depuis 7.21**. Sur TopSolid 7.20 tu auras une erreur de methode non trouvee."),
    # Permission warnings
    ("supprime le projet", "permission_warn", "Je ne peux pas supprimer un projet via l'Automation API — c'est une operation trop destructive. Utilise l'interface TopSolid directement."),
    ("format le disque", "out_of_scope", "Pas dans mon perimetre. Je ne fais que des operations TopSolid via l'Automation API."),
    # Parameter not found
    ("parametre Foo existe-t-il?", "param_check", "Je dois appeler `read_real_parameter` pour le verifier. S'il n'existe pas, tu auras une erreur 'Parameter not found'."),
    # Transaction rollback
    ("et si ca plante au milieu?", "rollback_explain", "En cas d'exception, il faut **imperativement** appeler `EndModification(false, false)` pour rollback, sinon le document reste en etat incoherent."),
    # Chaining context
    ("comment lire tous les parametres et leurs valeurs", "complex_chain", "Deux etapes : `read_parameters` pour lister les noms, puis `read_real_parameter` par nom pour chaque valeur. Veux-tu que je fasse la boucle ?"),
    # Graph navigation
    ("comment passer d'un DocumentId a un string", "graph_hint", "Chaine typique : `TopSolidHost.Documents.GetPdmObject(docId) -> TopSolidHost.Pdm.GetName(pdmObject)` qui retourne un string. Je peux te le generer."),
    # Common pitfall
    ("pourquoi mon ElementId est IsEmpty apres EnsureIsDirty", "pitfall_1", "Classique : `EnsureIsDirty` **change le docId** donc tous les ElementId obtenus AVANT deviennent invalides. Re-fetch-les APRES EnsureIsDirty."),
    # Event system
    ("ecouter les evenements", "events_warn", "L'API expose `IDocumentsEvents` mais l'ecoute en continu n'est pas supportee via le MCP (stateless). Utilise une app C# standalone pour les events."),
    # Geometry
    ("cree une cote d'angle", "geometry_pattern", "Usage typique : d'abord trouver les 2 entites (edges/faces), puis `Annotations.CreateAngleDimension(...)`. Cette operation necessite Pattern D."),
    # BOM caveats
    ("lis la nomenclature", "bom_caveats", "La BOM est calculee a la derniere reconstruction. Si le document est dirty, le resultat peut etre obsolete — fais un `rebuild_document` avant si besoin."),
    # Drafting specifics
    ("liste les vues d'un plan", "drafting_specifics", "Les vues sont des ElementId speciaux avec leur propre type (Projection/Section/Detail). Pour plus d'info : `api_help` avec 'drafting view'."),
    # Virtual documents
    ("mode virtuel c'est quoi", "virtual_explain", "Un document virtuel n'existe que en memoire (pas sauve sur disque). Utile pour tester sans polluer le projet. Attention : perdu si TopSolid ferme."),
    # Assembly constraints
    ("lis les contraintes d'assemblage", "asm_constraints", "Les contraintes sont des ElementId dans l'assemblage. `read_assembly_constraints` retourne la liste — chaque entree a un type (Coincidence, Distance, Angle, etc.)."),
    # Material
    ("change le materiau", "material_chain", "Deux etapes : le materiau doit exister dans la bibliotheque TopSolid (`IMaterials.Find...`). Puis on l'affecte via `set_material`."),
    # Shape selection
    ("selectionne une shape", "interactive_warn", "C'est une operation **interactive** : l'utilisateur doit cliquer dans TopSolid pour repondre. Si tu automatises, passe plutot par `read_shapes` + selection par nom."),
    # Parameter types
    ("types de parametres", "param_types", "TopSolid a 5 types : Real (unite SI), Integer, Text, Boolean, Enumeration. Le type dicte la recette d'acces."),
    # Position/Frame
    ("deplace la piece", "translate_hint", "Operations de translation : via `SetPosition(elementId, frame)`. Le `frame` est en metres et radians. Pense aux conversions SI."),
    # Export
    ("formats exports", "export_list", "Exports natifs : STEP, IGES, STL, PDF, DXF, 3D PDF, Image. Pour les formats specifiques (CAD externe) : `list_exporters`."),
    # History
    ("historique modif", "history_warn", "L'Automation n'expose pas l'historique detaille des modifications (pas de DAG). Seules les `read_revision_history` (majeures/mineures PDM) sont dispos."),
    # Units conversion display
    ("affichage en mm/deg", "display_units", "TopSolid STOCKE en SI mais affiche selon les unites du document. Pour etre sur : convertir toi-meme (×1000 mm, ×180/π deg)."),
    # Validation
    ("verifie que c'est bon", "validation_hint", "Pour valider une operation, enchaine : `read_*` avant, `set_*`, puis `read_*` apres pour confirmer. C'est du test A/B."),
    # Locking
    ("est-ce verrouille?", "lock_info", "`Pdm.IsLocked(pdmObject)` retourne true si un autre utilisateur a ouvert le doc. Si true, les ecritures echoueront."),
    # Search
    ("chercher dans le projet", "search_caveats", "`search_document` fait du CONTAINS sur le nom (insensible casse). Pour matcher le part_number, utilise `list_documents_without_reference` puis filter."),
    # Projection
    ("projection d'un plan", "projection_hint", "La projection principale (`read_main_projection`) definit la vue de face. Les autres vues (top, side, iso) sont calculees depuis elle."),
    # Templates
    ("templates dispo", "templates_info", "`read_document_template` retourne le template utilise a la creation. Pour un nouveau doc, il est choisi dans TopSolid, pas via l'API."),
    # Inclusion recursion
    ("sous-assemblages recursifs", "recursion_hint", "`list_inclusions` est recursif mais plat : les sous-sous-assemblages apparaissent. Utilise le `level` dans la reponse pour reconstruire l'arbre."),
    # Revision policy
    ("passer en revision majeure", "revision_policy", "Les passages de revision (A -> B) se font via TopSolid UI, pas via Automation. L'API ne fait que les lectures d'historique."),
    # Family instantiation
    ("creer instance de famille", "family_warn", "La creation d'instance se fait via TopSolid UI ou via `IFamilies.CreateInstance`. C'est transactionnel. Le driver values sont en SI."),
    # Error retry
    ("appeler plusieurs fois", "idempotency_warn", "La plupart des recettes READ sont idempotentes. Les WRITE NE LE SONT PAS (ex: `create_*` cree un nouvel element a chaque appel). Attention aux doublons."),
    # Performance
    ("c'est lent?", "perf_hint", "Certaines recettes sont lentes : `batch_*` iterent tous les documents du projet (~30s pour 50 docs). Pour des gros projets, passer par `list_*` + boucle ciblee."),
    # Metadata
    ("metadata du doc", "metadata_hint", "Les metadata PDM sont : name, designation, reference, manufacturer, revision, author, creation_date, last_modified. `read_pdm_properties` retourne tout."),
    # Coordinates
    ("coordonnees d'un point", "coord_hint", "Les Point3D sont en SI (metres). Pour l'afficher en mm : `(p.X * 1000, p.Y * 1000, p.Z * 1000)`. Idem pour Frame3D avec translation ET rotation (radians)."),
    # Bounding box
    ("bbox alignee ou orientee?", "bbox_hint", "`read_bounding_box` retourne la bbox **alignee aux axes du document**, pas l'OBB (oriented). L'OBB necessite un traitement custom."),
    # Features
    ("features = operations?", "features_vs_ops", "Non : `features` = primitives geometriques (trous, filetages). `operations` = arbre de construction complet (extrusions, fillet, etc). Les features sont un sous-ensemble."),
    # Multi-user
    ("plusieurs users en meme temps", "multi_user", "Le PDM TopSolid gere les verrous. Si un doc est ouvert par un autre user, l'API retourne 'locked'. Pas de merge automatique."),
    # Units
    ("autres unites?", "units_other", "`read_units` retourne les unites du document (longueur, angle, masse). Ne pas confondre avec les unites de STOCKAGE (toujours SI)."),
]


def gen_remarks(make_multi_turn, make_tool_call):
    """8) Model relays graph Remarks / critical knowledge."""
    entries = []
    for q, _tag, assistant in REMARKS_CASES:
        entries.append(make_multi_turn([
            ("user", q),
            ("assistant", assistant),
        ]))
    return entries


# ============================================================================
# MASTER
# ============================================================================

def generate_all_v6(make_multi_turn, make_tool_call):
    """Return dict of category -> entries, for easy stats reporting."""
    return {
        "multiturn_data (1a)": gen_multiturn_data(make_multi_turn, make_tool_call),
        "empty_results (1b)": gen_empty_results(make_multi_turn, make_tool_call),
        "acks (1c)": gen_acks(make_multi_turn, make_tool_call),
        "retries (2a)": gen_retries(make_multi_turn, make_tool_call),
        "not_found (2b)": gen_not_found(make_multi_turn, make_tool_call),
        "struct_errors (2c)": gen_structural_errors(make_multi_turn, make_tool_call),
        "formatted_returns (4)": gen_formatted_returns(make_multi_turn, make_tool_call),
        "chains (5)": gen_chains(make_multi_turn, make_tool_call),
        "followup (6)": gen_followup(make_multi_turn, make_tool_call),
        "write_acks (7)": gen_write_acks(make_multi_turn, make_tool_call),
        "remarks (8)": gen_remarks(make_multi_turn, make_tool_call),
    }
