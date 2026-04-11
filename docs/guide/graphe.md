# Graphe API

Le graphe API enrichi est le coeur du systeme. Il represente **toutes les methodes** de l'API TopSolid Automation sous forme de graphe oriente.

::: tip Visualisation interactive
Explorez le graphe en cliquant sur les noeuds : **[Graphe Interactif](/reference/graphe-interactif)**
:::

## Structure

```
[Source Type] --methodName()--> [Target Type]
```

Exemple :
```
DocumentId --GetElements()--> List<ElementId>
ElementId  --GetName()------> String
```

## Statistiques (2026-04-11)

| Metrique | Valeur |
|----------|--------|
| Edges totales | 4119 |
| Noeuds (types) | 242 |
| Methodes uniques | 1728 |
| Interfaces | 46 |
| Description | 90% |
| SemanticHint | 85% |
| Edges avec exemples reels | 1194 (29%) |
| Snippets de code | 2174 |

## Champs d'une edge

| Champ | Description | Exemple |
|-------|-------------|---------|
| `MethodName` | Nom de la methode | `GetParameters` |
| `MethodSignature` | Signature complete | `List<ElementId> GetParameters(DocumentId)` |
| `Interface` | Interface proprietaire | `IParameters` |
| `Description` | Documentation officielle (EN) | "Gets all parameters of a document" |
| `SemanticHint` | Mots-cles FR/EN pour recherche | "parametre, liste, document" |
| `Weight` | Priorite (1=important, 10=primitif, 20+=niche) | `2` |
| `Since` | Version minimum TopSolid | `v7.6` |
| `Examples` | Snippets C# reels (max 3) | Code de REDACTED-USER / Romain / Julien VB |

## Interfaces principales

| Interface | Methodes | Exemples | Description |
|-----------|----------|----------|-------------|
| IParameters | 161 | 178 | Parametres (lecture/ecriture valeurs, types, formules) |
| IPdm | 142 | 158 | Gestion documentaire (projets, documents, revisions) |
| IAssemblies | 59 | 88 | Assemblages (inclusions, occurrences, contraintes) |
| IDocuments | 72 | 76 | Operations sur documents (ouvrir, fermer, exporter) |
| IParts | 47 | 73 | Pieces (masse, inertie, derivation) |
| IDraftings | 31 | 57 | Mise en plan (vues, echelle, format, projection) |
| IFamilies | 43 | 45 | Familles (codes, catalogues, drivers) |
| ICoatings | 60 | 41 | Revetements (couleurs, textures) |
| ITables | 18 | 40 | Tableaux dans mises en plan |
| IShapes | 62 | 18 | Formes (faces, volume, extrusion) |
| IBoms | 24 | 19 | Nomenclatures (lignes, colonnes, contenu) |
| ISketches2D | 63 | 16 | Esquisses 2D (sommets, segments, profils) |
| IGeometries3D | 59 | 45 | Geometrie 3D (points, plans, reperes) |
| IElements | 48 | 33 | Elements (noms, types, proprietes) |

## Enrichissement

Le graphe est enrichi via le script `scripts/enrich-graph.py` qui execute 5 phases + injections manuelles :

1. **Phase 1** : Enrichit les edges existantes depuis `api-index.json` (descriptions, since)
2. **Phase 2** : Injecte les methodes absentes du graphe (64 methodes ajoutees)
3. **Phase 3** : Extrait des exemples depuis les fichiers .cs (REDACTED-USER + Romain + SelfLearning + KernelExamples)
4. **Phase 4** : Applique des regles semantiques manuelles (poids, hints critiques)
5. **Phase 5** : Auto-genere des hints FR depuis les descriptions (130+ traductions)
6. **Injections manuelles** : 54 methodes depuis les fichiers VB.NET de Julien (BOM, parametres, materiaux, textures, import, preview)
