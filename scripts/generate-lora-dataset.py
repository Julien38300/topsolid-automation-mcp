#!/usr/bin/env python3
"""
Generate LoRA fine-tuning dataset for the 3B TopSolid sub-agent.
Target: ministral-3:3b learns to select recipes by name from French user questions.

Output: data/lora-dataset.jsonl (ShareGPT/Axolotl compatible)
        data/lora-dataset-stats.json (statistics)

Sources:
  1. RecipeTool.cs — 85 recipes with descriptions
  2. SKILL.md — recipe mapping table
  3. graph.json — API methods with SemanticHint/Description
  4. api-index.json — method signatures
  5. help-md/FR/ — 2835 help pages
  6. Glossary + domain knowledge
  7. C# examples (REDACTED-USER + RoB)

Dataset structure (ShareGPT format):
{
  "conversations": [
    {"from": "system", "value": "..."},
    {"from": "human", "value": "..."},
    {"from": "gpt", "value": "..."}  // tool call or response
  ]
}
"""

import json
import os
import re
import glob
import random
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
DATA_DIR = SCRIPT_DIR.parent / "data"
RECIPES_FILE = SCRIPT_DIR.parent / "src" / "Tools" / "RecipeTool.cs"
SKILL_FILE = Path(r"C:\Users\jup\OneDrive\noemid-topsolid\skills\topsolid-mcp\SKILL.md")
GRAPH_FILE = DATA_DIR / "graph.json"
API_INDEX_FILE = DATA_DIR / "api-index.json"
HELP_DIR_FR = DATA_DIR / "help-md" / "FR"
GLOSSARY_FILE = Path(r"C:\Users\jup\.claude\projects\C--Users-jup-OneDrive-Cortana\memory\reference_topsolid_glossary_fr.md")
DOMAIN_FILE = Path(r"C:\Users\jup\.claude\projects\C--Users-jup-OneDrive-Cortana\memory\reference_topsolid_domain_knowledge.md")
EXAMPLES_AF = Path(r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples REDACTED-USER")
EXAMPLES_ROB = Path(r"C:\Users\jup\OneDrive\11_TopSolid_Expert\TrainingFiles\6 - Exemples Automation\Exemples RoB")

QA_EXPERT_DIR = DATA_DIR / "qa-topsolid-expert"
OUTPUT_FILE = DATA_DIR / "lora-dataset.jsonl"
STATS_FILE = DATA_DIR / "lora-dataset-stats.json"

SYSTEM_PROMPT = (
    "Tu es Noemid, assistant TopSolid. Tu utilises UNIQUEMENT mcp_topsolid_topsolid_run_recipe "
    "avec le nom d'une recette. Tu ne generes JAMAIS de code C#. Tu agis directement, sans demander confirmation."
)

# ============================================================================
# HELPERS
# ============================================================================

def make_entry(human_msg, assistant_msg, system=SYSTEM_PROMPT, category=""):
    """Create a ShareGPT conversation entry."""
    entry = {
        "conversations": [
            {"from": "system", "value": system},
            {"from": "human", "value": human_msg},
            {"from": "gpt", "value": assistant_msg}
        ]
    }
    if category:
        entry["category"] = category
    return entry


def make_tool_call(recipe, value=None):
    """Format a tool call response like the model should produce."""
    if value:
        return f'<tool_call>{{"name":"mcp_topsolid_topsolid_run_recipe","arguments":{{"recipe":"{recipe}","value":"{value}"}}}}</tool_call>'
    return f'<tool_call>{{"name":"mcp_topsolid_topsolid_run_recipe","arguments":{{"recipe":"{recipe}"}}}}</tool_call>'


def make_tool_call_api_help(query):
    """Format api_help tool call."""
    return f'<tool_call>{{"name":"mcp_topsolid_topsolid_api_help","arguments":{{"query":"{query}"}}}}</tool_call>'


def make_tool_call_get_state():
    """Format get_state tool call."""
    return '<tool_call>{"name":"mcp_topsolid_topsolid_get_state","arguments":{}}</tool_call>'


# ============================================================================
# CATEGORY 1: Recipe selection (core dataset)
# ============================================================================

def generate_recipe_selection_pairs():
    """Generate multiple French question variants for each recipe."""
    pairs = []

    # Manually crafted variants per recipe — the GOLD of the dataset
    recipe_variants = {
        # --- PDM Properties ---
        "lire_designation": [
            "c'est quoi la designation?",
            "quelle est la designation du document?",
            "designation de la piece",
            "montre-moi la designation",
            "donne la description du document",
            "lis la designation",
            "quelle est la desig?",
            "c'est quoi la description?",
            "donne moi la designation",
            "desig?",
        ],
        "lire_nom": [
            "c'est quoi le nom?",
            "quel est le nom du document?",
            "nom de la piece",
            "comment s'appelle ce document?",
            "donne le nom",
        ],
        "lire_reference": [
            "c'est quoi la reference?",
            "quel est le part number?",
            "reference de la piece",
            "numero de reference",
            "donne la ref",
            "reference pmd?",
            "part number svp",
            "quelle est la ref?",
            "c'est quoi le pn?",
        ],
        "lire_fabricant": [
            "c'est quoi le fabricant?",
            "quel est le fabricant?",
            "qui fabrique cette piece?",
            "fournisseur de la piece",
            "manufacturer",
        ],
        "lire_proprietes_pdm": [
            "montre toutes les proprietes",
            "quelles sont les proprietes PDM?",
            "toutes les infos PDM",
            "proprietes du document",
            "resume des proprietes",
        ],
        "modifier_designation": [
            ("change la designation en Bride", "Bride"),
            ("mets la designation a Support moteur", "Support moteur"),
            ("renomme la designation en Platine", "Platine"),
            ("nouvelle designation: Equerre", "Equerre"),
        ],
        "modifier_nom": [
            ("renomme le document en PIECE-001", "PIECE-001"),
            ("change le nom en Bride-Support", "Bride-Support"),
            ("nouveau nom: ASM-MOTEUR", "ASM-MOTEUR"),
        ],
        "modifier_reference": [
            ("change la reference en REF-2024-001", "REF-2024-001"),
            ("mets le part number a PN-500", "PN-500"),
            ("nouvelle ref: ABC-123", "ABC-123"),
        ],
        "modifier_fabricant": [
            ("change le fabricant en Schneider", "Schneider"),
            ("mets le fabricant a Bosch", "Bosch"),
            ("nouveau fournisseur: Siemens", "Siemens"),
        ],

        # --- Navigation ---
        "lire_projet_courant": [
            "c'est quoi le projet courant?",
            "quel projet est ouvert?",
            "dans quel projet on est?",
            "projet actif",
        ],
        "lire_contenu_projet": [
            "montre le contenu du projet",
            "qu'est-ce qu'il y a dans le projet?",
            "liste du projet",
        ],
        "chercher_document": [
            ("cherche le document Bride", "Bride"),
            ("trouve la piece Support", "Support"),
            ("ou est le document Platine?", "Platine"),
        ],
        "ouvrir_document_par_nom": [
            ("ouvre le document Bride", "Bride"),
            ("ouvre la piece Support", "Support"),
            ("affiche le document Equerre", "Equerre"),
        ],
        "lister_documents_projet": [
            "liste tous les documents du projet",
            "combien de documents dans le projet?",
            "montre tous les docs",
            "quels documents y a dans le projet?",
        ],

        # --- Parameters ---
        "lire_parametres": [
            "quels sont les parametres?",
            "liste des parametres",
            "montre les parametres",
            "combien de parametres?",
            "parametres du document",
        ],
        "lire_parametre_reel": [
            ("quelle est la valeur de Longueur?", "Longueur"),
            ("lis le parametre Hauteur", "Hauteur"),
            ("valeur de Largeur", "Largeur"),
            ("combien fait l'epaisseur?", "Epaisseur"),
        ],
        "lire_parametre_texte": [
            ("lis le parametre texte Type", "Type"),
            ("valeur texte de Categorie", "Categorie"),
        ],
        "modifier_parametre_reel": [
            ("mets Longueur a 100mm", "Longueur:0.1"),
            ("change Hauteur a 50mm", "Hauteur:0.05"),
            ("modifie Largeur a 200mm", "Largeur:0.2"),
            ("epaisseur a 3mm", "Epaisseur:0.003"),
        ],
        "modifier_parametre_texte": [
            ("change le parametre Type en Acier", "Type:Acier"),
            ("mets Categorie a Standard", "Categorie:Standard"),
        ],
        "comparer_parametres": [
            ("compare avec la piece Bride", "Bride"),
            ("difference avec Support", "Support"),
        ],

        # --- Mass/Volume/Dimensions ---
        "lire_masse_volume": [
            "c'est quoi la masse?",
            "quel est le poids?",
            "masse et volume",
            "combien pese cette piece?",
            "donne la masse",
            "volume de la piece",
            "masse volume surface",
            "poids de la piece",
        ],
        "rapport_masse_assemblage": [
            "masse totale de l'assemblage",
            "poids de l'assemblage",
            "rapport masse assemblage",
            "combien pese l'assemblage?",
            "masse totale",
            "masse asm",
            "combien pese cet assemblage?",
            "poids total",
            "donne moi le poids de l'ouvrage",
        ],
        "lire_densite_materiau": [
            "quelle est la densite?",
            "densite du materiau",
            "densite de la piece",
            "densite calculee?",
            "combien pour la densite?",
            "densite de la matiere",
        ],
        "lire_materiau": [
            "c'est quoi le materiau?",
            "quel materiau est affecte?",
            "materiau de la piece",
            "matiere utilisee",
            "c'est fait en quoi?",
            "matiere?",
            "quel est l'acier utilise?",
        ],
        "lire_dimensions_piece": [
            "quelles sont les dimensions?",
            "hauteur largeur longueur",
            "dimensions de la piece",
            "taille de la piece",
            "c'est quoi les cotes?",
        ],
        "lire_boite_englobante": [
            "boite englobante",
            "dimensions brut",
            "encombrement de la piece",
            "bounding box",
        ],
        "lire_moments_inertie": [
            "moments d'inertie",
            "inertie de la piece",
            "moments principaux",
        ],

        # --- Geometry ---
        "lire_points_3d": [
            "quels sont les points 3D?",
            "liste des points",
            "coordonnees des points",
        ],
        "lire_reperes_3d": [
            "quels sont les reperes?",
            "liste des reperes 3D",
            "frames de la piece",
        ],
        "lister_esquisses": [
            "quelles sont les esquisses?",
            "liste des esquisses",
            "combien d'esquisses?",
            "sketches de la piece",
        ],
        "lire_shapes": [
            "quels sont les shapes?",
            "liste des formes",
            "shapes de la piece",
            "solides de la piece",
        ],
        "lire_operations": [
            "quelles sont les operations?",
            "arbre des operations",
            "features de la piece",
            "historique de construction",
        ],

        # --- Colors/Attributes ---
        "attribut_lire_couleur": [
            "c'est quoi la couleur?",
            "quelle est la couleur de la piece?",
            "couleur du revetement",
            "quelle est la couleur de cet element?",
            "lis la couleur",
            "couleur svp",
            "donne moi la couleur",
        ],
        "attribut_lire_couleurs_faces": [
            "couleurs des faces",
            "quelles faces ont une couleur?",
            "couleurs individuelles des faces",
            "details des couleurs par face",
            "montre moi la couleur de chaque face",
        ],
        "attribut_modifier_couleur": [
            ("mets la piece en rouge", "255,0,0"),
            ("change la couleur en bleu", "0,0,255"),
            ("peins en vert", "0,128,0"),
            ("couleur jaune", "255,255,0"),
            ("passe en noir", "0,0,0"),
            ("gris", "128,128,128"),
            ("change la couleur", "255,128,0"),
            ("peins en orange", "255,165,0"),
            ("peins en", "255,0,0"),
            ("colorie", "0,255,0"),
            ("appliquer une couleur", "128,0,128"),
            ("met une couleur", "0,255,255"),
        ],
        "attribut_modifier_couleur_tout": [
            ("mets tout en gris", "128,128,128"),
            ("tout en vert", "0,255,0"),
            ("peins tous les elements en blanc", "255,255,255"),
            ("tout peindre en rouge", "255,0,0"),
            ("change la couleur de tout", "0,0,255"),
            ("colorise l'ensemble du doc", "255,255,0"),
            ("fait tout passer en bleu", "0,0,255"),
        ],
        "attribut_remplacer_couleur": [
            ("remplace le rouge par le bleu", "255,0,0:0,0,255"),
            ("change tout ce qui est vert en jaune", "0,255,0:255,255,0"),
            ("permuter couleur rouge vers vert", "255,0,0:0,255,0"),
            ("remplace le blanc par du noir", "255,255,255:0,0,0"),
            ("couleur source rouge vers destination bleu", "255,0,0:0,0,255"),
            ("passe du vert au jaune", "0,255,0:255,255,0"),
        ],
        "attribut_lire_tout": [
            "tous les attributs visuels",
            "couleur transparence calques",
            "dis moi tout sur l'aspect",
            "attributs de la piece",
            "proprietes visuelles",
            "donne moi les infos de couleur et calques",
        ],
        "attribut_lire_transparence": [
            "quelle est la transparence?",
            "transparence de la piece",
        ],
        "attribut_modifier_transparence": [
            ("mets la transparence a 50%", "0.5"),
            ("rends transparent a 80%", "0.8"),
            ("opaque", "0"),
        ],
        "attribut_lister_calques": [
            "quels sont les calques?",
            "liste des calques",
            "layers de la piece",
        ],
        "attribut_affecter_calque": [
            ("mets sur le calque 2", "2"),
            ("affecte au layer 5", "5"),
        ],

        # --- Assemblies ---
        "detecter_assemblage": [
            "c'est un assemblage?",
            "est-ce que c'est un assemblage?",
            "type de document assemblage?",
        ],
        "lister_inclusions": [
            "quelles sont les inclusions?",
            "liste des inclusions",
            "composants de l'assemblage",
        ],
        "lire_occurrences": [
            "quelles sont les occurrences?",
            "liste des occurrences",
            "instances dans l'assemblage",
        ],
        "compter_pieces_assemblage": [
            "combien de pieces?",
            "nombre de pieces dans l'assemblage",
            "combien de composants?",
            "part count",
            "combien de piece comporte l'assemblage courant?",
            "y'a combien de pieces?",
        ],
        "renommer_occurrence": [
            ("renomme l'occurrence Piece en Support", "Piece:Support"),
        ],

        # --- Export ---
        "exporter_step": [
            "exporte en STEP",
            "export STEP",
            "sauvegarde en STEP",
            "genere le STEP",
            "export 3D",
        ],
        "exporter_dxf": [
            "exporte en DXF",
            "export DXF",
            "genere le DXF",
            "mise a plat DXF",
        ],
        "exporter_pdf": [
            "exporte en PDF",
            "export PDF",
            "genere le PDF",
            "imprime en PDF",
        ],
        "exporter_stl": [
            "exporte en STL",
            "export STL",
            "pour impression 3D",
            "genere le STL",
            "impression 3D",
            "export stereolithographie",
            "genere le fichier STL",
            "exporte en .stl",
        ],
        "exporter_iges": [
            "exporte en IGES",
            "export IGES",
            "genere le IGES",
            "fichier .igs",
            "exporte en iges",
        ],
        "lister_exporteurs": [
            "quels formats d'export?",
            "formats disponibles",
            "liste des exporteurs",
        ],
        "exporter_nomenclature_csv": [
            "exporte la nomenclature en CSV",
            "BOM en CSV",
            "nomenclature CSV",
        ],

        # --- Audit ---
        "audit_piece": [
            "audit de la piece",
            "fais un audit",
            "verifie la piece",
            "diagnostic de la piece",
        ],
        "audit_assemblage": [
            "audit de l'assemblage",
            "verifie l'assemblage",
            "diagnostic assemblage",
        ],
        "verifier_piece": [
            "verification qualite",
            "controle qualite piece",
            "check la piece",
        ],
        "verifier_projet": [
            "verifie tout le projet",
            "audit du projet",
            "controle qualite projet",
        ],
        "verifier_materiaux_manquants": [
            "quelles pieces n'ont pas de materiau?",
            "materiaux manquants",
            "pieces sans matiere",
            "c'est quoi les pieces sans materiau?",
            "check les matieres",
            "oublis de matieres",
            "audit matieres",
        ],

        # --- Drafting ---
        "detecter_mise_en_plan": [
            "c'est une mise en plan?",
            "est-ce un plan?",
            "type mise en plan?",
        ],
        "lister_vues_mise_en_plan": [
            "quelles sont les vues?",
            "liste des vues du plan",
            "vues de la mise en plan",
        ],
        "lire_echelle_mise_en_plan": [
            "quelle est l'echelle?",
            "echelle du plan",
            "echelle des vues",
        ],
        "lire_format_mise_en_plan": [
            "quel est le format du plan?",
            "taille du papier",
            "format A3 ou A4?",
            "dimensions du plan",
        ],
        "lire_projection_principale": [
            "quelle est la projection principale?",
            "piece source du plan",
            "vues principales et auxiliaires",
        ],

        # --- BOM ---
        "detecter_nomenclature": [
            "c'est une nomenclature?",
            "est-ce un BOM?",
            "type nomenclature?",
        ],
        "lire_colonnes_nomenclature": [
            "quelles sont les colonnes?",
            "colonnes de la nomenclature",
            "structure du BOM",
        ],
        "lire_contenu_nomenclature": [
            "montre le contenu de la nomenclature",
            "affiche la nomenclature",
            "lis le BOM",
            "tableau de la nomenclature",
        ],
        "compter_lignes_nomenclature": [
            "combien de lignes dans la nomenclature?",
            "nombre de lignes BOM",
            "lignes actives nomenclature",
        ],

        # --- Unfolding ---
        "detecter_mise_a_plat": [
            "c'est une mise a plat?",
            "c'est un depliage?",
            "est-ce un deplie?",
            "type mise a plat?",
        ],
        "lire_plis_depliage": [
            "quels sont les plis?",
            "angles de pliage",
            "liste des plis",
            "rayons de pliage",
        ],
        "lire_dimensions_depliage": [
            "dimensions du depliage",
            "dimensions de la mise a plat",
            "taille du deplie",
            "epaisseur tolerie",
        ],

        # --- Document ---
        "type_document": [
            "c'est quoi comme type de document?",
            "quel type de document?",
            "piece, assemblage ou plan?",
        ],
        "sauvegarder_document": [
            "sauvegarde",
            "enregistre le document",
            "save",
        ],
        "reconstruire_document": [
            "reconstruis le document",
            "rebuild",
            "reconstruction",
        ],
        "sauvegarder_tout_projet": [
            "sauvegarde tout le projet",
            "enregistre tout",
            "save all",
            "enregistre tout le projet",
            "tout sauvegarder",
        ],
        "lire_propriete_utilisateur": [
            ("lis la propriete utilisateur Type de production", "Type de production"),
            ("valeur de Mise en plan necessaire", "Mise en plan necessaire"),
        ],
        "modifier_propriete_utilisateur": [
            ("change Type de production en Usine", "Type de production:Usine"),
        ],
        "invoquer_commande": [
            ("lance la commande ZoomAll", "ZoomAll"),
            ("execute la commande Reconstruction", "Reconstruction"),
        ],

        # --- Interactive (Ask*) ---
        "selectionner_shape": [
            "selectionne un shape",
            "je veux choisir une forme",
            "choisir un solide",
        ],
        "selectionner_face": [
            "selectionne une face",
            "je veux choisir une face",
            "choisir face",
        ],
        "selectionner_point_3d": [
            "selectionne un point",
            "je veux choisir un point 3D",
            "point 3D",
            "clique un point",
        ],

        # --- Family ---
        "detecter_famille": [
            "c'est une famille?",
            "est-ce un document famille?",
        ],
        "lire_codes_famille": [
            "quels sont les codes de la famille?",
            "colonnes catalogue famille",
        ],

        # --- Batch / Bibliotheque ---
        "lister_documents_dossier": [
            ("liste les documents du dossier Pieces", "Pieces"),
            ("montre le contenu du dossier Standard", "Standard"),
            ("qu'est-ce qu'il y a dans le dossier Tolerie?", "Tolerie"),
        ],
        "resumer_projet": [
            "resume du projet",
            "fais un resume",
            "combien de documents dans le projet?",
            "stats du projet",
            "c'est un gros projet?",
            "stats projet",
            "resume",
            "fais moi un bilan du projet",
            "bilan global du projet ouvert",
            "summary du projet",
            "synthese du projet courant",
        ],
        "compter_documents_par_type": [
            "combien de pieces, assemblages, plans?",
            "repartition par type",
            "documents par type",
            "combien de .TopPrt?",
            "repartition par type",
            "stats types",
            "nombre de documents de chaque sorte",
            "compte les types de fichiers",
            "combien de fichiers par extension?",
        ],
        "lister_documents_sans_reference": [
            "quels documents n'ont pas de reference?",
            "pieces sans part number",
            "documents sans ref",
            "audit references manquantes",
            "sans ref",
            "audit refs",
            "references manquantes",
        ],
        "lister_documents_sans_designation": [
            "quels documents n'ont pas de designation?",
            "pieces sans description",
            "documents sans designation",
            "audit designations manquantes",
            "sans designation",
            "audit designations",
            "designations vides",
        ],
        "chercher_pieces_par_materiau": [
            "liste les pieces avec leur masse",
            "quelles pieces sont les plus lourdes?",
            "masse de toutes les pieces",
            "pieces et materiaux du projet",
            "chercher par matiere",
            "quels sont les materiaux?",
        ],
        "lire_cas_emploi": [
            "ou est utilisee cette piece?",
            "cas d'emploi",
            "where-used",
            "qui utilise ce document?",
            "dans quels assemblages?",
            "back references",
            "where used",
            "ou est-ce que c'est utilise?",
            "qui utilise cette pièce?",
        ],
        "ouvrir_mise_en_plan": [
            "ouvre le plan",
            "ouvre la mise en plan",
            "montre le plan de cette piece",
            "switch vers le plan",
            "va sur la mise en plan",
        ],

        # --- NEW RECIPES (M-33 Expansion) ---

        # Misc/Folders
        "chercher_dossier": [
            ("cherche le dossier Travail", "Travail"),
            ("trouve le dossier Standard", "Standard"),
            ("ou est le dossier des equerres?", "Equerres"),
            ("cherche le repertoire 'Projets'", "Projets"),
            ("trouve moi le dossier 'Temp'", "Temp"),
            ("ou se trouve le dossier 'Client'?", "Client"),
        ],

        # Revisions
        "lire_historique_revisions": [
            "montre l'historique des revisions",
            "historique de revision",
            "quelles sont les revisions de ce document?",
            "liste les indices de revision",
            "donne l'historique",
            "revisions du doc svp",
            "c'est quoi l'historique des indices?",
            "montre moi toutes les versions du document",
        ],
        "comparer_revisions": [
            "compare avec la revision precedente",
            "qu'est-ce qui a change depuis la derniere rev?",
            "differences entre revs",
            "quelles sont les modifs par rapport a l'indice d'avant?",
            "cherche les differences avec la rev prec",
            "ecart entre revisions",
        ],

        # Batch Operations (PDM)
        "exporter_batch_step": [
            "exporte tout le projet en STEP",
            "genere les STEP de tous les docs",
            "batch export STEP",
            "toutes les pieces en step",
            "fais un export massif en step",
            "genere les stp de tout le projet",
        ],
        "lire_propriete_batch": [
            ("lis la propriete Auteur sur tout le projet", "Auteur"),
            ("recupere le Code sur tout le projet", "Code"),
        ],
        "chercher_documents_modifies": [
            "quel documents ont ete modifies recemment?",
            "docs modifies",
            "cherche les documents changes",
        ],
        "vider_auteur_batch": [
            "vide le champ auteur sur tout le projet",
            "nettoie l'auteur sur les docs",
            "reset auteur batch",
        ],
        "vider_auteur_document": [
            "vide le champ auteur du document",
            "reset auteur",
        ],
        "verifier_virtuel_batch": [
            "verifie les documents virtuels du projet",
            "audit virtuels batch",
        ],
        "activer_virtuel_batch": [
            "active le mode virtuel sur tout le projet",
            "tout en virtuel",
        ],
        "activer_virtuel_document": [
            "active le mode virtuel sur ce document",
            "passe en virtuel",
        ],

        # Family Drivers
        "verifier_drivers_famille": [
            "verifie les drivers de la famille",
            "audit drivers famille",
        ],
        "corriger_drivers_famille": [
            "corrige les drivers de la famille",
            "fix drivers famille",
        ],
        "verifier_drivers_famille_batch": [
            "verifie les drivers de toutes les familles du projet",
            "audit drivers batch",
        ],

        # Audits
        "auditer_noms_parametres": [
            "verifie le nom des parametres",
            "audit noms parametres",
        ],
        "auditer_noms_parametres_batch": [
            "verifie les noms de parametres sur tout le projet",
            "audit noms params batch",
        ],
        "auditer_designations_drivers_batch": [
            "verifie les designations des drivers sur tout le projet",
            "audit drivers designations batch",
        ],

        # (Fusionne ci-dessus)

        # Comparison Expansion
        "comparer_operations_documents": [
            ("compare les operations avec Bride", "Bride"),
            ("quelles differences d'operations avec Support?", "Support"),
            ("ecart d'operations avec la piece 'Base'", "Base"),
            ("comparaison arbre construction avec 'ASM-01'", "ASM-01"),
            ("est-ce que les operations sont les memes que sur 'Equerre'?", "Equerre"),
        ],
        "comparer_entites_documents": [
            ("compare les entites avec Support", "Support"),
            ("differences geometriques avec Base", "Base"),
            ("compare la geometrie avec 'Platine'", "Platine"),
            ("ecart de shapes avec 'Support V2'", "Support V2"),
            ("verifie les entites par rapport a 'Couvercle'", "Couvercle"),
        ],
        "reporter_parametres": [
            ("reporte les parametres de cette piece sur Bride", "Bride"),
            ("copie les params vers ASM-100", "ASM-100"),
            ("clonne les parametres sur 'Platine'", "Platine"),
            ("injecte les valeurs sur 'Equerre'", "Equerre"),
            ("reporte les variables sur 'Support'", "Support"),
        ],
        "reporter_proprietes_pdm": [
            ("reporte les infos PDM vers Support", "Support"),
            ("copie designation et ref vers Document-B", "Document-B"),
            ("clonne designation/ref sur 'Piece 2'", "Piece 2"),
            ("reporte les proprietes sur 'Base'", "Base"),
            ("copie fabricant et reference sur 'Couvercle'", "Couvercle"),
        ],
    }

    for recipe, variants in recipe_variants.items():
        for v in variants:
            if isinstance(v, tuple):
                question, value = v
                pairs.append(make_entry(
                    question,
                    make_tool_call(recipe, value),
                    category="recipe_selection"
                ))
            else:
                pairs.append(make_entry(
                    v,
                    make_tool_call(recipe),
                    category="recipe_selection"
                ))

    return pairs


# ============================================================================
# CATEGORY 2: Clarification (when to ask before acting)
# ============================================================================

def generate_clarification_pairs():
    """Generate pairs where the model should ask for clarification."""
    pairs = []

    clarifications = [
        ("change la couleur", "Quel element ? Tout le document ou un shape specifique ?"),
        ("renomme", "Tu veux changer le nom PDM, la designation, ou la reference ?"),
        ("exporte", "En quel format ? STEP, DXF, PDF, STL, IGES ?"),
        ("modifie le parametre", "Quel parametre et quelle valeur ?"),
        ("verifie", "La piece seule ou tout le projet ?"),
        ("supprime", "Supprimer quoi ? Un parametre, un element, ou le document ?"),
        ("change la valeur", "Quel parametre et quelle nouvelle valeur ?"),
        ("mets a jour", "Qu'est-ce que tu veux mettre a jour exactement ?"),
        ("affiche les infos", "Quelles infos ? Proprietes PDM, parametres, geometrie, masse ?"),
        # Smart Params Ambiguity
        ("parametre longueur", "Tu souhaites lire la valeur du parametre 'longueur' ou la modifier ?"),
        ("parametre hauteur", "Est-ce pour lire la hauteur ou la modifier ?"),
        ("la largeur", "Veux-tu consulter la largeur actuelle ou en affecter une nouvelle ?"),
        ("epaisseur", "Tu veux lire l'epaisseur ou changer sa valeur ?"),
        ("le rayon", "Souhaites-tu connaitre le rayon ou le modifier ?"),
        ("angle", "C'est pour lire l'angle ou lui donner une nouvelle valeur ?"),
    ]

    for question, answer in clarifications:
        pairs.append(make_entry(question, answer, category="clarification"))

    return pairs


# ============================================================================
# CATEGORY 2.5: Domain Knowledge (QA Expert)
# ============================================================================

def generate_domain_knowledge_pairs():
    """Load QA pairs from expert JSON files, filtering score >= 4."""
    pairs = []
    if not QA_EXPERT_DIR.exists():
        print(f"  [!] Warning: {QA_EXPERT_DIR} not found. Skipping domain knowledge.")
        return []

    json_files = glob.glob(str(QA_EXPERT_DIR / "*.json"))
    total_found = 0
    total_kept = 0

    for fpath in json_files:
        try:
            with open(fpath, "r", encoding="utf-8") as f:
                data = json.load(f)
                for item in data:
                    total_found += 1
                    if item.get("score", 0) >= 4:
                        pairs.append(make_entry(
                            item["question"],
                            item["answer"],
                            category="domain_knowledge"
                        ))
                        total_kept += 1
        except Exception as e:
            print(f"  [!] Error reading {fpath}: {e}")

    print(f"  -> Found {total_found} QA pairs, kept {total_kept} (score >= 4)")
    return pairs


# ============================================================================
# CATEGORY 3: Multi-turn conversations
# ============================================================================

def generate_multi_turn_pairs():
    """Generate multi-turn conversation examples."""
    pairs = []

    # Conversation 1: Check state then act
    pairs.append({
        "conversations": [
            {"from": "system", "value": SYSTEM_PROMPT},
            {"from": "human", "value": "quel est le projet courant dans TopSolid?"},
            {"from": "gpt", "value": make_tool_call_get_state()},
            {"from": "tool", "value": '{"project":"Test MCP","document":"Bride-Support","version":720400043,"connected":true}'},
            {"from": "gpt", "value": "Le projet courant est Test MCP, document Bride-Support."},
            {"from": "human", "value": "ok et la masse?"},
            {"from": "gpt", "value": make_tool_call("lire_masse_volume")},
            {"from": "tool", "value": "Masse: 2.450 kg\nVolume: 312500.00 mm3\nSurface: 45000.00 mm2"},
            {"from": "gpt", "value": "Masse: 2.450 kg, Volume: 312500 mm3, Surface: 45000 mm2."},
        ],
        "category": "multi_turn"
    })

    # Conversation 2: Export workflow
    pairs.append({
        "conversations": [
            {"from": "system", "value": SYSTEM_PROMPT},
            {"from": "human", "value": "exporte en STEP puis en PDF"},
            {"from": "gpt", "value": make_tool_call("exporter_step")},
            {"from": "tool", "value": "Export STEP OK: C:\\Temp\\Bride-Support.step"},
            {"from": "gpt", "value": "Export STEP OK. Maintenant le PDF."},
            {"from": "human", "value": ""},
            {"from": "gpt", "value": make_tool_call("exporter_pdf")},
            {"from": "tool", "value": "Export PDF OK: C:\\Temp\\Bride-Support.pdf"},
            {"from": "gpt", "value": "Export PDF OK. Les deux fichiers sont dans C:\\Temp\\."},
        ],
        "category": "multi_turn"
    })

    # Conversation 3: Assembly investigation
    pairs.append({
        "conversations": [
            {"from": "system", "value": SYSTEM_PROMPT},
            {"from": "human", "value": "combien de pieces dans l'assemblage?"},
            {"from": "gpt", "value": make_tool_call("compter_pieces_assemblage")},
            {"from": "tool", "value": "=== COUNT PIECES ===\n 3x Piece\n 1x Piece Simple\nTotal: 4 pieces (2 references uniques)"},
            {"from": "gpt", "value": "4 pieces (2 references uniques): 3x Piece, 1x Piece Simple."},
            {"from": "human", "value": "et la masse totale?"},
            {"from": "gpt", "value": make_tool_call("rapport_masse_assemblage")},
            {"from": "tool", "value": "=== RAPPORT MASSE ASSEMBLAGE ===\nMasse totale: 9.922 kg\nVolume total: 1263904.82 mm3\nSurface totale: 115447.61 mm2\nNombre de pieces: 4"},
            {"from": "gpt", "value": "Masse totale: 9.922 kg, 4 pieces."},
        ],
        "category": "multi_turn"
    })

    # Conversation 4: Modification workflow
    pairs.append({
        "conversations": [
            {"from": "system", "value": SYSTEM_PROMPT},
            {"from": "human", "value": "change la designation en Bride de fixation"},
            {"from": "gpt", "value": make_tool_call("modifier_designation", "Bride de fixation")},
            {"from": "tool", "value": "OK: Designation modifiee de 'Bride' en 'Bride de fixation'."},
            {"from": "gpt", "value": "Designation modifiee: Bride de fixation."},
            {"from": "human", "value": "et la reference en BR-2024-001"},
            {"from": "gpt", "value": make_tool_call("modifier_reference", "BR-2024-001")},
            {"from": "tool", "value": "OK: Reference modifiee en 'BR-2024-001'."},
            {"from": "gpt", "value": "Reference modifiee: BR-2024-001."},
        ],
        "category": "multi_turn"
    })

    return pairs


# ============================================================================
# CATEGORY 4: Error handling
# ============================================================================

def generate_error_handling_pairs():
    """Generate pairs showing proper error responses."""
    pairs = []

    errors = [
        ("genere un script C# pour creer un cylindre",
         "Je ne genere pas de code C#. Je peux executer des recettes pre-definies. Que veux-tu faire exactement ?"),
        ("ecris du code pour lire les parametres",
         "Je n'ecris pas de code. Utilise la recette lire_parametres. Tu veux que je la lance ?"),
        ("fais un FEA sur cette piece",
         "Les simulations FEA ne sont pas disponibles via les recettes actuelles."),
        ("connecte-toi a un autre serveur TopSolid",
         "Je ne peux pas modifier la connexion TopSolid. Le serveur MCP est configure sur le port 8090."),
        ("supprime tout le projet",
         "Je ne peux pas supprimer de projets. Cette operation est trop risquee pour une recette automatique."),
    ]

    for question, answer in errors:
        pairs.append(make_entry(question, answer, category="error_handling"))

    return pairs


# ============================================================================
# CATEGORY 5: Domain vocabulary (FR/EN TopSolid terms)
# ============================================================================

def generate_vocabulary_pairs():
    """Generate pairs that teach TopSolid domain vocabulary."""
    pairs = []

    vocab = [
        ("c'est quoi une designation dans TopSolid?",
         "La designation est la description du document (colonne 'Designation'). C'est different du nom. Pour la lire: lire_designation. Pour la modifier: modifier_designation."),
        ("quelle difference entre nom et designation?",
         "Le nom est l'identifiant du document (colonne 'Nom', ex: PIECE-001). La designation est la description (colonne 'Designation', ex: Bride de fixation). Deux recettes differentes: lire_nom et lire_designation."),
        ("c'est quoi une mise au coffre?",
         "Mise au coffre = CheckIn dans TopSolid. C'est l'operation qui verrouille un document apres modification."),
        ("c'est quoi un revetement?",
         "Le revetement (coating) est l'apparence de la piece: couleur, transparence. Recettes: lire_couleur_piece, modifier_couleur_piece."),
        ("c'est quoi une rafale?",
         "Une rafale est la generation automatique de nomenclatures ou de plans pour un ensemble de documents. C'est un batch dans le vocabulaire TopSolid."),
        ("c'est quoi une inclusion?",
         "Une inclusion est un composant place dans un assemblage. Chaque inclusion a une definition (la piece source) et peut avoir des occurrences. Recette: lister_inclusions."),
        ("c'est quoi un ensemble de projection?",
         "Un ensemble de projection est un groupe de vues dans une mise en plan. Il contient une vue principale et des vues auxiliaires. Recette: lire_projection_principale."),
        ("difference entre piece et assemblage?",
         "Une piece (.TopPrt) est un document avec de la geometrie directe. Un assemblage (.TopAsm) contient des inclusions d'autres documents. Recette: detecter_assemblage ou type_document."),
        ("c'est quoi une mise a plat?",
         "Une mise a plat est le depliage d'une piece de tolerie. Elle montre la forme a plat avant pliage, avec les lignes de pli. Utilisee pour la decoupe laser/DXF. Recette: detecter_mise_a_plat."),
        ("les unites dans TopSolid?",
         "TopSolid utilise le systeme SI: longueurs en metres (50mm = 0.05), angles en radians (45deg = 0.785), masses en kg."),
        ("c'est quoi un cas d'emploi?",
         "Le cas d'emploi (where-used / back-reference) montre dans quels assemblages ou plans une piece est utilisee. Recette: lire_cas_emploi."),
        ("c'est quoi un .TopPrd?",
         "Un .TopPrd est un document propriete utilisateur dans TopSolid. Il definit une enumeration custom (ex: Type de production, Type de composant). On le cree et l'affecte via CreateUserPropertyParameter."),
        ("quels sont les types de documents TopSolid?",
         ".TopPrt = Piece, .TopAsm = Assemblage, .TopDft = Mise en plan, .TopMat = Materiau, .TopTex = Texture, .TopPrd = Propriete utilisateur. Recette: type_document ou compter_documents_par_type."),
        ("comment verifier la qualite d'un projet?",
         "Plusieurs recettes: lister_documents_sans_reference pour les refs manquantes, lister_documents_sans_designation pour les designations vides, verifier_materiaux_manquants pour les matieres, audit_piece pour une piece specifique."),
        ("comment trouver les pieces les plus lourdes?",
         "Utilise chercher_pieces_par_materiau pour lister toutes les pieces avec leur masse. Les masses sont lues depuis les parametres systeme de chaque piece."),
    ]

    for question, answer in vocab:
        pairs.append(make_entry(question, answer, category="vocabulary"))

    return pairs


# ============================================================================
# CATEGORY 6: API knowledge from graph (sampled)
# ============================================================================

def generate_api_knowledge_pairs():
    """Generate pairs from graph.json SemanticHints and Descriptions."""
    pairs = []

    if not GRAPH_FILE.exists():
        print(f"  [SKIP] graph.json not found at {GRAPH_FILE}")
        return pairs

    with open(GRAPH_FILE, "r", encoding="utf-8") as f:
        graph = json.load(f)

    edges = graph.get("_edges", [])
    if isinstance(edges, dict):
        edges = list(edges.values())

    # Sample edges with good SemanticHint
    hinted = []
    for edge in edges:
        hint = edge.get("SemanticHint", "")
        desc = edge.get("Description", "")
        method = edge.get("MethodName", "")
        iface = edge.get("Interface", "")
        if hint and desc and method:
            hinted.append((method, iface, hint, desc))

    # Sample up to 200
    random.seed(42)
    sample = random.sample(hinted, min(200, len(hinted)))

    for method, iface, hint, desc in sample:
        question = f"a quoi sert {iface}.{method} dans TopSolid?"
        answer = f"{hint}. {desc}"
        pairs.append(make_entry(question, answer, category="api_knowledge"))

    print(f"  [API] {len(sample)} API knowledge pairs from {len(hinted)} hinted edges")
    return pairs


# ============================================================================
# CATEGORY 7: Unit conversion examples
# ============================================================================

def generate_unit_conversion_pairs():
    """Generate pairs showing SI unit conversions for TopSolid."""
    pairs = []

    conversions = [
        ("change la hauteur a 50mm", make_tool_call("modifier_parametre_reel", "Hauteur:0.05")),
        ("mets la longueur a 1 metre", make_tool_call("modifier_parametre_reel", "Longueur:1.0")),
        ("epaisseur 3mm", make_tool_call("modifier_parametre_reel", "Epaisseur:0.003")),
        ("largeur 250mm", make_tool_call("modifier_parametre_reel", "Largeur:0.25")),
        ("angle a 45 degres", make_tool_call("modifier_parametre_reel", "Angle:0.785398")),
        ("angle a 90 degres", make_tool_call("modifier_parametre_reel", "Angle:1.5708")),
        ("rayon 10mm", make_tool_call("modifier_parametre_reel", "Rayon:0.01")),
    ]

    for question, answer in conversions:
        pairs.append(make_entry(question, answer, category="unit_conversion"))

    return pairs


# ============================================================================
# CATEGORY 8: Augmented variants (synonyms, typos, informal)
# ============================================================================

def generate_augmented_variants():
    """Generate augmented question variants using templates + synonyms."""
    pairs = []

    # Templates: (prefix patterns) x (recipe keywords) -> recipe
    templates = {
        "lire_masse_volume": [
            "donne moi la masse", "dis moi le poids", "la masse?", "le poids?",
            "tu peux me dire la masse?", "j'ai besoin de la masse",
            "c'est quoi le volume de la piece?", "combien pese-t-elle?",
            "masse svp", "poids de cette piece", "la piece fait combien?",
            "volume et surface", "surface de la piece", "proprietes physiques",
            "mass", "weight",
        ],
        "compter_pieces_assemblage": [
            "y a combien de pieces la dedans?", "pieces dans l'asm?",
            "nombre de composants", "nb pieces", "combien de parts?",
            "c'est compose de combien de pieces?", "pieces total",
            "liste moi le nombre de pieces", "part count",
            "combien de piece comporte l'assemblage?",
            "combien y a t il de pieces dans cet assemblage?",
            "dis moi combien de pieces", "nb composants",
        ],
        "lire_designation": [
            "la desig?", "desig svp", "c koi la designation",
            "description du doc", "designation stp",
            "designation courante", "quelle desig?",
        ],
        "lire_parametres": [
            "les params?", "parametres svp", "liste params",
            "montre les params", "quels params?",
            "c koi les parametres?", "affiche les parametres",
            "params du doc", "donnees parametriques",
        ],
        "exporter_step": [
            "fais un step", "step svp", "genere moi un step",
            "j'ai besoin du step", "cree le fichier step",
            "sortie STEP", "step export", "export 3D step",
            "sauvegarde en step AP214",
        ],
        "exporter_pdf": [
            "fais un pdf", "pdf svp", "genere le pdf",
            "j'ai besoin du pdf", "imprime en pdf",
            "sortie pdf", "pdf du plan",
        ],
        "exporter_dxf": [
            "fais un dxf", "dxf svp", "genere le dxf",
            "dxf pour laser", "dxf de la mise a plat",
            "fichier dxf", "sortie dxf",
        ],
        "rapport_masse_assemblage": [
            "masse asm", "poids total asm", "masse de l'asm",
            "combien pese l'assemblage au total?",
            "poids assemblage complet", "rapport de masse",
            "masse de tout l'ensemble",
        ],
        "lire_proprietes_pdm": [
            "toutes les props", "props pdm", "infos pdm",
            "resume du document", "fiche du document",
            "proprietes completes", "tout sur ce doc",
        ],
        "audit_piece": [
            "fais un check", "check la piece", "diagnostique",
            "verifie cette piece", "audit svp", "controle",
            "est-ce que la piece est ok?", "y a des erreurs?",
        ],
        "lister_inclusions": [
            "quoi dedans?", "qu'est-ce qu'il y a dans l'asm?",
            "contenu de l'assemblage", "inclusions?",
            "composants?", "c'est fait de quoi?",
        ],
        "detecter_assemblage": [
            "c'est un asm?", "assemblage ou piece?",
            "quel type c'est?", "c'est quoi comme doc?",
        ],
        "lire_materiau": [
            "c'est en quoi?", "quelle matiere?", "materiau?",
            "c'est quel metal?", "matiere de la piece",
            "acier? alu? inox?",
        ],
        "lire_dimensions_piece": [
            "les dimensions?", "taille?", "hauteur largeur?",
            "c'est quoi les cotes globales?", "encombrement?",
            "la piece fait combien en dimensions?",
        ],
        "sauvegarder_document": [
            "save", "sauvegarde", "enregistre", "ctrl s",
            "sauvegarde le doc", "save svp",
        ],
        "modifier_couleur_piece": [
            ("mets en rouge", "255,0,0"),
            ("passe en bleu", "0,0,255"),
            ("couleur verte", "0,128,0"),
            ("peins en jaune", "255,255,0"),
            ("noir svp", "0,0,0"),
            ("mets en orange", "255,165,0"),
            ("blanc", "255,255,255"),
            ("gris clair", "192,192,192"),
        ],
        "detecter_mise_en_plan": [
            "c'est un plan?", "plan ou piece?",
            "mise en plan?", "c'est un dessin?",
        ],
        "detecter_nomenclature": [
            "c'est un bom?", "nomenclature?",
            "c'est une nomenclature?", "BOM?",
        ],
        "lire_contenu_nomenclature": [
            "montre la nomenclature", "affiche le bom",
            "contenu du bom", "lis la nomenclature",
            "qu'est-ce qu'il y a dans la nomenclature?",
        ],
        "detecter_mise_a_plat": [
            "c'est un deplie?", "mise a plat?",
            "depliage?", "c'est une tolerie depliee?",
        ],
        "reconstruire_document": [
            "rebuild", "reconstruis", "reconstruction",
            "recalcule le document", "mets a jour la geo",
        ],
        "lire_operations": [
            "arbre de construction", "features?",
            "operations de la piece", "historique",
            "comment la piece est construite?",
        ],
        "lister_esquisses": [
            "les esquisses?", "sketches?", "profils?",
            "liste les esquisses", "combien d'esquisses?",
        ],
    }

    for recipe, variants in templates.items():
        for v in variants:
            if isinstance(v, tuple):
                question, value = v
                pairs.append(make_entry(question, make_tool_call(recipe, value), category="augmented"))
            else:
                pairs.append(make_entry(v, make_tool_call(recipe), category="augmented"))

    return pairs


# ============================================================================
# CATEGORY 9: Context-dependent questions
# ============================================================================

def generate_context_pairs():
    """Generate pairs where context (piece vs assemblage vs plan) matters."""
    pairs = []

    context_qs = [
        # Questions that work on any document type
        ("c'est quoi le type du document?", make_tool_call("type_document")),
        ("quel genre de document c'est?", make_tool_call("type_document")),

        # Natural follow-ups after state check
        ("ok et les parametres?", make_tool_call("lire_parametres")),
        ("et la designation?", make_tool_call("lire_designation")),
        ("maintenant la masse", make_tool_call("lire_masse_volume")),
        ("et le fabricant?", make_tool_call("lire_fabricant")),
        ("et la reference?", make_tool_call("lire_reference")),
        ("montre les proprietes", make_tool_call("lire_proprietes_pdm")),
        ("exporte", "En quel format ? STEP, DXF, PDF, STL, IGES ?"),
        ("et en step?", make_tool_call("exporter_step")),
        ("pdf aussi", make_tool_call("exporter_pdf")),
        ("dxf maintenant", make_tool_call("exporter_dxf")),
        ("sauvegarde apres", make_tool_call("sauvegarder_document")),

        # Implicit document type questions
        ("quelles vues y a dans le plan?", make_tool_call("lister_vues_mise_en_plan")),
        ("montre l'echelle", make_tool_call("lire_echelle_mise_en_plan")),
        ("c'est quoi le format? A3? A4?", make_tool_call("lire_format_mise_en_plan")),
        ("affiche les lignes du bom", make_tool_call("lire_contenu_nomenclature")),
        ("les plis?", make_tool_call("lire_plis_depliage")),
    ]

    for question, answer in context_qs:
        pairs.append(make_entry(question, answer, category="contextual"))

    return pairs


# ============================================================================
# CATEGORY 10: Negative examples (what NOT to do)
# ============================================================================

def generate_negative_pairs():
    """Generate examples of incorrect behavior to avoid."""
    pairs = []

    negatives = [
        # Don't generate code
        ("ecris un script pour lire les parametres",
         "Je n'ecris pas de code. La recette lire_parametres fait exactement ca. Tu veux que je la lance ?"),
        ("genere du C# pour modifier la couleur",
         "Je ne genere pas de code C#. Dis-moi juste quelle couleur tu veux et je lance la recette."),
        ("code pour exporter en STEP",
         "Pas besoin de code, la recette exporter_step le fait directement. Je lance ?"),

        # Don't hallucinate
        ("la piece fait combien de kilos?", make_tool_call("lire_masse_volume")),
        # (Not "La piece fait 3.5 kg" without calling the tool)

        # Don't describe what you'll do
        ("masse de l'assemblage", make_tool_call("rapport_masse_assemblage")),
        # (Not "Je vais lancer un script qui va...")

        # Don't ask for confirmation on simple reads
        ("designation?", make_tool_call("lire_designation")),
        # (Not "Tu veux que je lise la designation ?")

        # Multi-format requests
        ("exporte en step et pdf", make_tool_call("exporter_step")),
        # Model should do STEP first, then PDF in next turn
    ]

    for question, answer in negatives:
        pairs.append(make_entry(question, answer, category="negative_examples"))

    return pairs


# ============================================================================
# MAIN
# ============================================================================

def main():
    print("=" * 60)
    print("LoRA Dataset Generator for TopSolid 3B Sub-Agent")
    print("=" * 60)

    all_pairs = []

    # Category 1: Recipe selection (core)
    print("\n[1/10] Generating recipe selection pairs...")
    recipe_pairs = generate_recipe_selection_pairs()
    all_pairs.extend(recipe_pairs)
    print(f"  ->{len(recipe_pairs)} pairs")

    # Category 2: Clarification
    print("[2/10] Generating clarification pairs...")
    clarif_pairs = generate_clarification_pairs()
    all_pairs.extend(clarif_pairs)
    print(f"  ->{len(clarif_pairs)} pairs")

    # Category 2.5: Domain Knowledge (QA Expert)
    print("[2.5/10] Loading domain knowledge from JSON files...")
    domain_pairs = generate_domain_knowledge_pairs()
    all_pairs.extend(domain_pairs)
    print(f"  ->{len(domain_pairs)} pairs")

    # Category 3: Multi-turn
    print("[3/10] Generating multi-turn conversations...")
    multi_pairs = generate_multi_turn_pairs()
    all_pairs.extend(multi_pairs)
    print(f"  ->{len(multi_pairs)} conversations")

    # Category 4: Error handling
    print("[4/10] Generating error handling pairs...")
    error_pairs = generate_error_handling_pairs()
    all_pairs.extend(error_pairs)
    print(f"  ->{len(error_pairs)} pairs")

    # Category 5: Vocabulary
    print("[5/10] Generating vocabulary pairs...")
    vocab_pairs = generate_vocabulary_pairs()
    all_pairs.extend(vocab_pairs)
    print(f"  ->{len(vocab_pairs)} pairs")

    # Category 6: API knowledge
    print("[6/10] Generating API knowledge pairs from graph...")
    api_pairs = generate_api_knowledge_pairs()
    all_pairs.extend(api_pairs)
    print(f"  ->{len(api_pairs)} pairs")

    # Category 7: Unit conversions
    print("[7/10] Generating unit conversion pairs...")
    unit_pairs = generate_unit_conversion_pairs()
    all_pairs.extend(unit_pairs)
    print(f"  ->{len(unit_pairs)} pairs")

    # Category 8: Augmented variants
    print("[8/10] Generating augmented variants...")
    aug_pairs = generate_augmented_variants()
    all_pairs.extend(aug_pairs)
    print(f"  ->{len(aug_pairs)} pairs")

    # Category 9: Contextual
    print("[9/10] Generating contextual pairs...")
    ctx_pairs = generate_context_pairs()
    all_pairs.extend(ctx_pairs)
    print(f"  ->{len(ctx_pairs)} pairs")

    # Category 10: Negative examples
    print("[10/10] Generating negative examples...")
    neg_pairs = generate_negative_pairs()
    all_pairs.extend(neg_pairs)
    print(f"  ->{len(neg_pairs)} pairs")

    # Shuffle
    random.seed(42)
    random.shuffle(all_pairs)

    # Write JSONL
    print(f"\nWriting {len(all_pairs)} entries to {OUTPUT_FILE}...")
    os.makedirs(DATA_DIR, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        for entry in all_pairs:
            f.write(json.dumps(entry, ensure_ascii=False) + "\n")

    # Stats
    categories = {}
    for entry in all_pairs:
        cat = entry.get("category", "unknown")
        categories[cat] = categories.get(cat, 0) + 1

    stats = {
        "total_entries": len(all_pairs),
        "categories": categories,
        "recipes_count": 112,
        "format": "ShareGPT (Axolotl/Unsloth compatible)",
        "system_prompt_length": len(SYSTEM_PROMPT),
        "generated_at": "2026-04-11",
        "version": "v1.1",
        "target_model": "ministral-3b",
        "sources": [
            "RecipeTool.cs (112 recipes)",
            "SKILL.md v4 (recipe mappings)",
            "QA TopSolid Expert (16 JSON files, score >= 4)",
            "graph.json (4119 edges, 200 sampled)",
            "Domain vocabulary (manual)",
            "Unit conversions (manual)",
        ]
    }

    with open(STATS_FILE, "w", encoding="utf-8") as f:
        json.dump(stats, f, indent=2, ensure_ascii=False)

    print(f"\n{'=' * 60}")
    print(f"Dataset: {OUTPUT_FILE}")
    print(f"Stats:   {STATS_FILE}")
    print(f"Total:   {len(all_pairs)} entries")
    for cat, count in sorted(categories.items()):
        print(f"  {cat}: {count}")
    print(f"{'=' * 60}")


if __name__ == "__main__":
    main()
