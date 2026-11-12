# Contribuer

Le projet grandit avec ce que les utilisateurs de TopSolid decouvrent dans leur travail : une recette qui fait gagner une heure, un piege de l'API, une correction, un essai sur une autre version de TopSolid. **Pas besoin d'ecrire du C# pour contribuer.**

| Vous avez... | Faites ceci | TopSolid requis |
|---|---|---|
| un bug ou un plantage | [Signaler un bug](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=bug_report.md), ou *Signaler un bug* dans le menu de l'icone : version, systeme et etat de connexion sont remplis pour vous | non |
| une tache que vous voudriez en une recette | [Demander une recette](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_request.yml) | non |
| un script qui a marche dans `topsolid_execute_script` | [Le proposer comme recette](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=recipe_proposal.yml), ou ouvrir une pull request | oui, pour le tester |
| trouve un piege de l'API (unite, appel refuse, boite de dialogue qui bloque...) | [Partager un piege d'API](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=api_pitfall.yml) : il rejoint les skills des agents | non |
| fait tourner le serveur sur une version ou une configuration non listee | [Signaler la compatibilite](https://github.com/Julien38300/topsolid-automation-mcp/issues/new?template=compatibility_report.yml) | oui |

Les questions et les retours d'usage vont dans les [Discussions](https://github.com/Julien38300/topsolid-automation-mcp/discussions). Les taches pour commencer portent le label [`good first issue`](https://github.com/Julien38300/topsolid-automation-mcp/labels/good%20first%20issue).

## Les regles

1. **Uniquement du materiel public** : l'aide publique de TopSolid (help.topsolid.com), le SDK Automation installe avec TopSolid et vos propres essais. Jamais de documentation non publique, de code ou de donnees de votre employeur, de fichiers, noms de pieces ou chemins d'affaires clientes.
2. **Signez vos commits** (DCO) : `git commit -s` ajoute `Signed-off-by: Votre Nom <vous@exemple.com>` et certifie que vous avez le droit de soumettre la modification sous licence MIT. Une verification echoue sur les pull requests dont les commits ne sont pas signes.
3. **Ni secret ni chemin personnel** : la CI lance `scripts/privacy-scan.py` sur chaque pull request.
4. **Le code est en anglais** (noms et descriptions des recettes, identifiants, commentaires) ; ce site est en francais, les pages en anglais sont bienvenues.

## Ajouter une recette, en bref

1. Mettre au point le script en direct avec `topsolid_compile` puis `topsolid_execute_script`.
2. L'ajouter dans `server/src/Tools/RecipeTool.cs` avec le bon mode : `R` lecture, `RW` modification du modele (transaction et sauvegarde automatiques), `RN` appels PDM ou document seulement (hors transaction), `RD` ecriture de fichiers.
3. Ajouter son nom dans `server/data/recipe-list.txt`, a la meme place.
4. `python scripts/check-recipes.py`, puis regenerer `server/data/recipes.md` avec `server/scripts/gen-recipes-catalog.py`.
5. Dans la pull request, dire sur quelle version de TopSolid, sur quel document, et ce que la recette a rendu.

Le detail (installation, regles du C# 5, tests, skills, documentation) est dans le [guide de contribution](https://github.com/Julien38300/topsolid-automation-mcp/blob/main/CONTRIBUTING.md), en anglais.

## Ce qui vient

Le parcours de contribution s'ameliore par etapes : une recette par fichier, un kit de test public, des skills relus par la CI. Voir la [roadmap](./roadmap).
