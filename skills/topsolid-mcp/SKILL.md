---
name: topsolid-mcp
description: Pilote TopSolid via MCP. Utilise ce skill pour TOUTE question TopSolid (etat, designation, parametres, export, esquisses, assemblages).
version: 2.0.0
metadata:
  hermes:
    tags: [topsolid, cao, cad, pdm, mcp, automation]
    trigger_phrases: ["topsolid", "piece", "assemblage", "esquisse", "parametre", "designation", "reference", "fabricant", "export", "step", "dxf", "pdf", "nomenclature", "mise en plan", "mise a plat"]
---

# TopSolid MCP — Skill de pilotage

## REGLE ABSOLUE

Tu ne connais PAS l'API TopSolid. Tu NE GENERES JAMAIS de code C#.
Tu utilises UNIQUEMENT l'outil `mcp_topsolid_topsolid_run_recipe` avec le nom d'une recette.

## Workflow (2 etapes max)

### Etape 1 — Etat TopSolid
Appelle `mcp_topsolid_topsolid_get_state` pour verifier la connexion.

### Etape 2 — Executer une recette
Appelle `mcp_topsolid_topsolid_run_recipe` avec le bon nom de recette.

## Mapping question → recette

| L'utilisateur demande | recipe | value |
|---|---|---|
| designation, description | lire_designation | |
| nom du document | lire_nom | |
| reference, part number | lire_reference | |
| fabricant, fournisseur | lire_fabricant | |
| toutes les proprietes | lire_proprietes_pdm | |
| changer la designation | modifier_designation | la nouvelle designation |
| renommer | modifier_nom | le nouveau nom |
| parametres, dimensions | lire_parametres | |
| exporteurs, formats | lister_exporteurs | |
| type de document | type_document | |

## Exemples

Question: "Quelle est la designation de la piece ?"
→ `mcp_topsolid_topsolid_run_recipe` recipe="lire_designation"

Question: "Change la designation en Ma Piece"
→ `mcp_topsolid_topsolid_run_recipe` recipe="modifier_designation" value="Ma Piece"

Question: "Liste moi les parametres"
→ `mcp_topsolid_topsolid_run_recipe` recipe="lire_parametres"

Question: "C'est quel type de document ?"
→ `mcp_topsolid_topsolid_run_recipe` recipe="type_document"

## Si la question ne correspond a aucune recette

Utilise `mcp_topsolid_topsolid_api_help` avec un mot-cle pour chercher.
Puis reponds avec l'information trouvee. NE GENERE PAS de code.

## Glossaire

| Francais TopSolid | Signification |
|---|---|
| Designation | Description du document (pas le nom) |
| Reference | Part number |
| Mise au coffre | CheckIn |
| Sorti de coffre | CheckOut |
