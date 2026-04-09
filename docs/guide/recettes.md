# Recettes

68 scripts C# prets a l'emploi, documentes avec pieges et patterns. Chaque recette peut etre envoyee directement via `topsolid_execute_script` ou `topsolid_modify_script`.

## Par categorie

### PDM / Navigation (R-001..R-010)
| # | Description | Pattern |
|---|-------------|---------|
| R-001 | Ouvrir un document par nom | READ |
| R-003 | Naviguer dans l'arbre PDM recursif | READ |
| R-009 | Renommer un document | WRITE |
| R-010 | Lire metadata PDM (nom, designation, reference, fabricant) | READ |

### Parametres (R-002, R-005..R-014)
| # | Description | Pattern |
|---|-------------|---------|
| R-002 | Modifier un parametre reel | WRITE |
| R-005 | Creer un parametre reel | WRITE |
| R-006 | Copier parametres entre documents | WRITE |
| R-008 | Supprimer un parametre | WRITE |
| R-011 | Modifier parametre texte | WRITE |
| R-012 | Modifier parametre booleen | WRITE |
| R-013 | Modifier parametre enumeration | WRITE |
| R-014 | Creer parametre avec formule SmartReal | WRITE |

### Export / Import (R-004, R-015..R-019, R-072..R-074)
| # | Description | Format |
|---|-------------|--------|
| R-004 | Exporter en STEP | STEP |
| R-015 | Exporter avec options (FBX, glTF, Parasolid) | Multi |
| R-016 | Importer STEP avec options | STEP |
| R-019 | Lister tous les exporteurs/importeurs | — |
| R-072 | Exporter en DXF | DXF |
| R-073 | Exporter en PDF | PDF |
| R-074 | Exporter en IFC | IFC |

### Geometrie / Esquisses (R-030..R-034)
| # | Description | Pattern |
|---|-------------|---------|
| R-030 | Creer une esquisse sur le plan XY | WRITE |
| R-031 | Creer un rectangle dans une esquisse | WRITE |
| R-032 | Creer une extrusion depuis une esquisse | WRITE |
| R-033 | Lire les points 3D du document | READ |
| R-034 | Creer un point 3D | WRITE |

### Assemblages (R-035..R-036, R-043..R-046)
| # | Description | Pattern |
|---|-------------|---------|
| R-035 | Detecter si le document est un assemblage | READ |
| R-036 | Lister les inclusions et occurrences | READ |
| R-043 | Detecter assemblage + lister inclusions | READ |
| R-044 | Inclusion parametree avec pilotes | WRITE |
| R-045 | Contrainte frame-on-frame | WRITE |
| R-046 | Modifier code/pilotes d'une inclusion | WRITE |

### Familles (R-037..R-038, R-049..R-050b)
| # | Description | Pattern |
|---|-------------|---------|
| R-037 | Creer une famille explicite | WRITE |
| R-038 | Lire les codes et instances d'une famille | READ |
| R-049 | Contraintes et conditions famille | READ |
| R-050b | Colonnes catalogue famille | READ |

### Proprietes utilisateur (R-075)
| # | Description | Pattern |
|---|-------------|---------|
| R-075 | Lire/ecrire une propriete utilisateur | READ/WRITE |

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

Le fichier complet des recettes est dans `TopSolidMcpServer/data/recipes.md`.
