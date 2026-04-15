# Document de test TopSolid — Reference

Fichier prepare par Julien le 2026-04-04.
Ce document doit etre ouvert dans TopSolid pendant l'execution de la test suite.

## Identite

| Champ | Valeur |
|-------|--------|
| Nom du document | Test 01 |
| Nom du projet | Test 01 |
| Type | **Assemblage** |
| Author | JuP |
| Standard | ISO |
| Description | Test 01 |
| Major Revision | A |
| Minor Revision | 0 |

## Esquisses (3)

| # | Nom |
|---|-----|
| 1 | Sketch 1 |
| 2 | Sketch 2 |
| 3 | Sketch 3 |

## Parametres utilisateur (non-systeme)

| Nom | Type | Valeur |
|-----|------|--------|
| Parametre 1 | Real | 10 |
| Mise en plan necessaire | Boolean | True |
| Type de production | Text/Enum | Manufactured |
| Union | Text | "" (vide) |
| Union? | Formula | 0 (= when(length($'Part Number')>0;1;0)) |

## Parametres systeme (acces direct via API)

| Nom | Valeur | Methode API probable |
|-----|--------|---------------------|
| Name | "Test 01" | IDocuments.GetName(docId) |
| Description | "Test 01" | IDocuments.GetDescription(docId) |
| Mass | 0kg | via IProperties ou system param |
| Volume | 0mm3 | via IProperties ou system param |
| Surface Area | 0mm2 | via IProperties ou system param |
| Type for BOM | Composite | system param |
| Part Count | 0 | system param |
| Standard | ISO | system param |
| Author | JuP | system param |
| Major Revision | A | IDocuments.GetMajorRevision(docId) |
| Minor Revision | 0 | IDocuments.GetMinorRevision(docId) |

## Valeurs attendues pour TestSuite.json

### T-01 get_state connexion
- contains: "Version"
- Version > 0

### T-02 get_state document actif
- contains: "Test 01"

### T-03 api_help "sketch"
- contains: "ISketches2D" ou "GetSketches" ou "Sketch"

### T-04 api_help "IParameters"
- contains: "GetRealValue" ou "GetTextValue"

### T-07 execute_script simple (Version)
- contains: "Version"
- not contains: "Erreur"

### T-08 execute_script document name
- contains: "Test 01"
- not contains: "Erreur"

### T-09 execute_script pattern LLM (using + Run())
- contains: un resultat valide (Version ou autre)
- not contains: "Erreur de compilation"

### T-10 execute_script pattern LLM (using seuls)
- contains: un resultat valide
- not contains: "Erreur de compilation"

### Tests supplementaires possibles

- Test assemblage : IsAssembly → doit retourner true (c'est un assemblage)
- Test parametres : lister les parametres → doit contenir "Parametre 1"
- Test esquisses : lister les esquisses → doit contenir "Sketch 1", "Sketch 2", "Sketch 3"
