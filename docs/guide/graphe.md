# Graphe API

Le graphe API enrichi est le coeur du systeme. Il represente **toutes les methodes** de l'API TopSolid Automation sous forme de graphe oriente.

## Structure

```
[Source Type] --methodName()--> [Target Type]
```

Exemple :
```
DocumentId --GetElements()--> List<ElementId>
ElementId  --GetName()------> String
```

## Statistiques (2026-04-19)

| Metrique | Valeur |
|----------|--------|
| Edges totales | 4119 |
| Noeuds (types) | 242 |
| Methodes uniques | 1728 |
| Interfaces | 46 |
| Description | 90% |
| SemanticHint | 84% |
| Exemples (.cs reels) | 22% (926 edges) |

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
| `Examples` | Snippets C# reels (max 3) | Code provenant de corpora prives locaux (non redistribues) |

## Interfaces principales

| Interface | Methodes | Description |
|-----------|----------|-------------|
| IParameters | 161 | Parametres (lecture/ecriture valeurs, types, formules) |
| IPdm | 139 | Gestion documentaire (projets, documents, revisions) |
| IDocuments | 64 | Operations sur documents (ouvrir, fermer, exporter) |
| ISketches2D | 59 | Esquisses 2D (sommets, segments, profils) |
| IGeometries3D | 59 | Geometrie 3D (points, plans, reperes, frames) |
| IShapes | 62 | Formes (faces, edges, volume, extrusion) |
| IAssemblies | 56 | Assemblages (inclusions, occurrences, contraintes) |
| ISketches3D | 52 | Esquisses 3D |
| IElements | 48 | Elements (noms, types, proprietes) |
| IFamilies | 43 | Familles (codes, catalogues, instances) |

## Enrichissement

Le graphe est enrichi via le script `scripts/enrich-graph.py` qui execute 5 phases :

1. **Phase 1** : Enrichit les edges existantes depuis `api-index.json` (descriptions, since)
2. **Phase 2** : Injecte les methodes absentes du graphe (64 methodes ajoutees)
3. **Phase 3** : Extrait des exemples depuis des fichiers .cs locaux (corpora prives de l'auteur)
4. **Phase 4** : Applique des regles semantiques manuelles (poids, hints critiques)
5. **Phase 5** : Auto-genere des hints FR depuis les descriptions (130+ traductions)
