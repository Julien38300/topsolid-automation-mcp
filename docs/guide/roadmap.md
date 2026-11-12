# Roadmap

## Ou en est le projet (v1.8.18, novembre 2026)

- **15 outils MCP**, dont `topsolid_run_recipe` (219 recettes), `topsolid_api_help` (1728 methodes, avec les remarques officielles de transaction), `topsolid_compare_documents` (comparer deux revisions et reporter l'ecart) et `topsolid_detect_dialog`.
- **Endpoint HTTP natif** avec cle `X-API-Key`, icone de notification, mise a jour depuis les releases GitHub, interface en francais ou en anglais selon Windows.
- **Aide en ligne** indexee (5809 pages) et **catalogue des commandes** de l'interface (2428 commandes).
- Teste sur TopSolid 7.20 et 7.21 ; signale aussi en fonctionnement sous TopSolinux.

Le detail version par version est dans les [notes de release](https://github.com/Julien38300/topsolid-automation-mcp/releases).

## Prochaines etapes

### Ouvrir le projet aux contributions

| Etape | Contenu |
|---|---|
| Fait (v1.8.18+) | Guide de contribution, formulaires d'issue (bug, demande de recette, proposition de recette, piege d'API, compatibilite), modele de pull request, signature DCO, signalement de securite prive, Discussions |
| Une recette par fichier | Sortir les recettes du fichier unique `RecipeTool.cs` : un fichier par recette avec son en-tete (mode, categorie, versions testees, auteur), catalogues generes et verifies par la CI sans TopSolid |
| Kit de test public | Un projet TopSolid de reference publie, et un script qui lance une recette sur le TopSolid du contributeur et produit un rapport a joindre a la pull request |
| Skills communs | Le depot devient la reference des skills, avec un controle CI (liens, recettes citees qui existent) ; publication du skill `topsolid-api-knowledge` (pieges de l'API) |
| Credits | Contributeurs cites dans chaque note de release ; matrice de compatibilite par version de TopSolid alimentee par les retours |

### Fonctions

| Sujet | Contenu |
|---|---|
| Confirmation avant les operations destructives | [#7](https://github.com/Julien38300/topsolid-automation-mcp/issues/7) |
| Graphe API 7.21 | Ajouter au graphe les methodes d'interface apparues en 7.21 |
| TopSolid'Cam | Couvrir l'API Cam Automation (recettes et aide) |
| Documentation en anglais | Traduire les pages du guide |

Une idee ? Ouvrez une [discussion](https://github.com/Julien38300/topsolid-automation-mcp/discussions) ou une [demande de recette](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_request.yml).
