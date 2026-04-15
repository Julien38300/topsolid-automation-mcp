# Recettes

113 recettes pre-construites dans `RecipeTool`. Le LLM selectionne par nom via `topsolid_run_recipe` -- aucune generation de code necessaire.

## Tableau interactif

Recherche, tri par colonne et filtres par categorie/mode.

<RecipeTable />

## Pattern de modification

Toutes les recettes WRITE suivent le meme pattern :

```csharp
TopSolidHost.Application.StartModification("Description", false);
TopSolidHost.Documents.EnsureIsDirty(ref docId);
// ... modifications ...
TopSolidHost.Application.EndModification(true, true);
TopSolidHost.Pdm.Save(pdmId, true);
```

::: danger
`EnsureIsDirty(ref docId)` change le `docId` ! Chercher les elements **APRES** cet appel, jamais avant.
:::

## Couleurs de reference

| Nom | RGB |
|-----|-----|
| rouge | `255,0,0` |
| vert | `0,128,0` |
| bleu | `0,0,255` |
| jaune | `255,255,0` |
| orange | `255,165,0` |
| blanc | `255,255,255` |
| noir | `0,0,0` |
| gris | `128,128,128` |

## Unites (TopSolid = SI)

| Grandeur | Unite SI | Conversion |
|----------|---------|------------|
| Longueurs | metres | 50 mm = `0.05` |
| Angles | radians | 45° = `0.785398` |
| Masses | kg | |
| Volumes | m³ | Affiche en mm³ (×10⁹) |
| Surfaces | m² | Affiche en mm² (×10⁶) |

## Tests LIVE

59/61 tests PASS sur TopSolid vivant (assemblage REF-NOEMID-TEST).

| Categorie | PASS | Total |
|-----------|------|-------|
| PDM read/write | 6 | 6 |
| Assemblage | 6 | 6 |
| Export (STEP/STL/IGES/DXF/PDF) | 5 | 5 |
| Attributs lecture | 5 | 5 |
| Parametres | 1 | 1 |
| Geometrie | 1 | 1 |
| Projet | 1 | 1 |

21 recettes non testees automatiquement (Ask* interactives, contexte specifique requis).

## Dataset LoRA

732 paires d'entrainement dans `data/lora-dataset.jsonl` pour fine-tuner le sous-agent 3B. Script regenerable : `scripts/generate-lora-dataset.py`.
