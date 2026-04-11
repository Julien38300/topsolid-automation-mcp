# Cross-Thread: Cortana -> Noemid
Derniere mise a jour: 2026-04-10

## Dataset LoRA pret pour M-33

Le dataset d'entrainement LoRA pour le sous-agent TopSolid 3B est genere.

### Fichiers

| Fichier | Chemin | Contenu |
|---------|--------|---------|
| Dataset | `TopSolidMcpServer/data/lora-dataset.jsonl` | 509 paires ShareGPT |
| Stats | `TopSolidMcpServer/data/lora-dataset-stats.json` | Statistiques |
| Script | `TopSolidMcpServer/scripts/generate-lora-dataset.py` | Regenerable |

### Composition du dataset (v3)

| Categorie | Paires | Description |
|-----------|--------|-------------|
| recipe_selection | 309 | Question FR -> tool_call run_recipe (COEUR) |
| api_knowledge | 200 | "A quoi sert X?" -> hint + description |
| clarification | 9 | Quand demander clarification |
| unit_conversion | 7 | Conversion mm/deg -> SI (metres/radians) |
| vocabulary | 15 | Termes metier TopSolid (designation, inclusion, types documents, etc.) |
| error_handling | 5 | Refus de generer du code, limites |
| multi_turn | 4 | Conversations completes avec tool_call + reponse |
| augmented | 158 | Variantes informelles/synonymes/typos |
| contextual | 18 | Follow-ups naturels + questions dependantes du type de doc |
| negative_examples | 7 | Ce que le modele ne doit PAS faire |
| **Total** | **732** | |

### Format

```json
{
  "conversations": [
    {"from": "system", "value": "Tu es Noemid, assistant TopSolid..."},
    {"from": "human", "value": "combien de pieces?"},
    {"from": "gpt", "value": "<tool_call>{\"name\":\"mcp_topsolid_topsolid_run_recipe\",\"arguments\":{\"recipe\":\"compter_pieces_assemblage\"}}</tool_call>"}
  ],
  "category": "recipe_selection"
}
```

Compatible Axolotl / Unsloth (format ShareGPT).

### Architecture Hermes actuelle

- **Main agent** : ministral 8B (routing, raisonnement, conversation)
- **Sous-agent TopSolid** : ministral 3B (execution recettes via MCP) <- CIBLE LoRA
- Le 3B selectionne les recettes mais perd le contexte apres 2-3 tours
- Le LoRA doit lui apprendre: mapping FR -> recipe, vocabulaire TopSolid, format tool_call

### Sources disponibles pour enrichir le dataset

| Source | Taille | Utilise dans v1 |
|--------|--------|-----------------|
| RecipeTool.cs | 93 recettes | OUI (274 paires) |
| graph.json | 4119 edges, 3491 hinted | OUI (200 echantillonnees) |
| api-index.json | 1462 methodes | NON (a ajouter) |
| help-md/FR/ | 2835 pages | NON (trop gros, a echantillonner) |
| Exemples C# AF | 21 fichiers | NON (pour code generation future) |
| Exemples C# RoB | 709 fichiers | NON (pour code generation future) |
| Glossaire FR | 73 termes | OUI (10 paires) |
| Domain knowledge | 78 lignes | OUI (integre) |

### Prochaines etapes (M-33 / M-34)

1. **Enrichir le dataset** : augmenter les variantes de questions (objectif: 1000+ paires recipe_selection)
2. **Ajouter aide en ligne** : echantillonner 200-500 pages FR les plus pertinentes
3. **Tester le fine-tuning** : Axolotl sur ministral-3:3b avec le dataset actuel
4. **Evaluer** : mesurer le taux de selection correcte de recettes sur un test set
5. **Iterer** : ajouter les erreurs du modele fine-tune comme exemples negatifs

### Etat MCP/Recettes (depuis Cortana)

- 93 recettes couvrant: PDM, navigation, parametres, masse/volume/dimensions, geometrie, attributs, assemblages, export (6 formats), audit, mise en plan, nomenclature, mise a plat
- 59/61 tests LIVE PASS
- Graphe: 4119 edges, 1728 methodes, 84% hints, 90% descriptions
- SKILL.md v4 avec 7 few-shot examples
