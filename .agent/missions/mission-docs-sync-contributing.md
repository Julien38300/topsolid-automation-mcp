# Mission — Sync docs + Guide contributeur

**Date** : 2026-04-15
**Repo** : noemid-topsolid-automation (C:\Users\jup\OneDrive\noemid-topsolid)
**Objectif** : Fusionner le contenu manquant depuis cortana, ajouter la doc contributeur

Ce repo est la SOURCE UNIQUE pour les docs VitePress. Ne rien modifier dans cortana.

---

## ETAPE 1 — Fusionner recettes.md

Le fichier `docs/guide/recettes.md` de **ce repo** (noemid) contient seulement le tableau interactif (68 lignes).
Le fichier equivalent dans cortana (`C:\Users\jup\OneDrive\Cortana\TopSolidMcpServer\docs\guide\recettes.md`) contient 309 lignes avec des sections supplementaires apres le tableau :

- "Par categorie (detail)" — tableaux par categorie avec colonnes Recette, Description, Technique, Mode
- "Pattern de modification" — le pattern transactionnel obligatoire
- "Couleurs TopSolid" — codes couleur ARGB
- "Unites SI" — rappel metres/radians
- "Tests" — comment lancer les tests

**Action** : Copie les sections supplementaires de cortana APRES le `<RecipeTable />` dans le recettes.md de noemid. Ne touche PAS au contenu existant de noemid (le texte d'intro et le composant `<RecipeTable />`).

**Ne PAS copier** les tableaux de recettes par categorie mot pour mot — ils font doublon avec le RecipeTable.vue interactif. Copie UNIQUEMENT les sections de reference :
- Pattern de modification (snippet C#)
- Couleurs TopSolid
- Unites SI
- Tests

---

## ETAPE 2 — Creer CONTRIBUTING.md

Creer `CONTRIBUTING.md` a la racine du repo noemid. Ce guide explique comment un contributeur peut ajouter une nouvelle recette. Contenu :

```markdown
# Contribuer a TopSolid MCP

## Ajouter une recette

Une recette est un script C# pre-construit que le LLM peut appeler par nom via `topsolid_run_recipe`.

### 1. Ajouter la recette dans le serveur

Fichier : `server/src/Tools/RecipeTool.cs`

Chaque recette est un dictionnaire `{ "nom_recette", R("description", @"script C#") }` pour READ
ou `{ "nom_recette", RW("description", @"script C# avec {value}") }` pour WRITE.

Pattern READ :
```csharp
{ "ma_nouvelle_recette", R(
    "Description courte en francais",
    @"
    DocumentId docId = TopSolidHost.Documents.EditedDocument;
    // ... logique de lecture ...
    return ""resultat"";
    "
) }
```

Pattern WRITE (modification) — TOUJOURS suivre le pattern transactionnel :
```csharp
{ "ma_recette_write", RW(
    "Description courte en francais",
    @"
    DocumentId docId = TopSolidHost.Documents.EditedDocument;
    TopSolidHost.Application.StartModification(""Description"", false);
    TopSolidHost.Documents.EnsureIsDirty(ref docId);
    // *** docId a potentiellement CHANGE ici ***
    // ... modifications avec le nouveau docId ...
    TopSolidHost.Application.EndModification(true, true);
    PdmObjectId pdmId = TopSolidHost.Documents.GetPdmObject(docId);
    TopSolidHost.Pdm.Save(pdmId, true);
    return ""OK"";
    "
) }
```

Le parametre `{value}` est remplace automatiquement par l'argument utilisateur.

### 2. Ajouter la recette dans la documentation

Fichier : `docs/.vitepress/theme/components/RecipeTable.vue`

Ajouter une entree dans le tableau `recipes` :
```javascript
{ name: "ma_nouvelle_recette", description: "Description fonctionnelle detaillee en francais", mode: "READ", category: "MaCategorie", api: "Interface.Methode" },
```

Categories existantes : PDM, Navigation, Parametres, Geometrie, Esquisse, Assemblage, Materiau, Physique, Attribut, Mise en plan, Export, Comparaison, Audit, Famille, Etat

### 3. Tester

```powershell
cd tests
.\run-tests.ps1
```

Les tests necessitent TopSolid 7 actif avec un projet ouvert.

### 4. Conventions

- Noms de recettes : `verbe_objet` en francais (ex: `lire_masse_volume`, `modifier_designation`)
- Descriptions : en francais, langage metier TopSolid
- Code : en anglais (variables, commentaires inline)
- Toujours retourner un string (jamais void)
- Toujours gerer le cas "pas de document actif" avec un message clair

### 5. Build

```bash
# Serveur MCP
cd server && dotnet build TopSolidMcpServer.sln

# Documentation
cd docs && npm install && npm run dev
```
```

---

## ETAPE 3 — Mettre a jour le CLAUDE.md

Dans `CLAUDE.md` a la racine de noemid :

1. Remplacer la ligne "L'agent Hermes se connecte au MCP server via stdio." par "L'agent TopSolid se connecte via OpenClaw (sous-agent dedie avec system.md + tool scoping)."
2. Ajouter une section "## Contribuer" qui pointe vers CONTRIBUTING.md
3. Verifier que la structure du repo decrite est a jour

---

## ETAPE 4 — Mettre a jour README.md

Dans `README.md` :

1. Ajouter un lien vers CONTRIBUTING.md dans une section "## Contribuer"
2. Mettre a jour le nombre de recettes si necessaire (113 actuellement)
3. S'assurer que le lien vers le site docs fonctionne

---

## ETAPE 5 — Ajouter .gitignore pour les artefacts build du graph

Le repo noemid contient des fichiers `bin/` et `obj/` dans `graph/src/TopSolidApiGraph.Core/`.
Ajouter dans `.gitignore` (s'il ne les couvre pas deja) :

```
**/bin/
**/obj/
```

---

## Validation

- [ ] `recettes.md` contient le tableau interactif + les sections reference (pattern, couleurs, unites)
- [ ] `CONTRIBUTING.md` existe avec le guide complet d'ajout de recette
- [ ] `CLAUDE.md` ne mentionne plus Hermes
- [ ] `README.md` pointe vers CONTRIBUTING.md
- [ ] `.gitignore` couvre bin/ et obj/
- [ ] `npm run dev` dans docs/ fonctionne toujours (pas de regression)
