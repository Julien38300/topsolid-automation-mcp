# Installation TopSolid MCP — guide noob

**Tout se fait en double-clic. Aucune ligne de commande à taper.**

## Installation en 3 clics

1. **Double-clic sur `Installer_TopSolidMCP.bat`**
   - Installe Node.js automatiquement si absent (via winget)
   - Installe les composants du bridge
   - **Génère la clé API automatiquement** (48 caractères aléatoires, affichée et copiée dans le presse-papiers) — rien à inventer ; une clé existante est conservée
   - Nettoie toute ancienne installation héritée (tâche planifiée système `TopSolidMcpBridge`) qui bloquerait le bridge
   - Enregistre le démarrage automatique à l'ouverture de session

2. **Cherche l'icône près de l'horloge** (systray) : c'est TopSolid MCP.

3. **C'est tout.** Le bridge tourne, surveillé automatiquement.

## L'icône systray

Clic droit sur l'icône :

| Menu | Effet |
|---|---|
| **Statut** | Vérifie que le bridge tourne (le relance s'il est arrêté) |
| **Redémarrer** | Redémarre le bridge |
| **Arrêter** | Coupe le bridge |
| **Paramètres → Clé API...** | Affiche la clé actuelle (pré-remplie) ; laisser **vide** génère une nouvelle clé |
| **Paramètres → Régénérer la clé API** | Révoque immédiatement l'ancienne clé, en génère une nouvelle et la copie dans le presse-papiers |
| **Quitter** | Arrête le bridge + l'icône |

## Comportement automatique

- **Au démarrage du PC** : le bridge repart tout seul (raccourci enregistré)
- **Toutes les 30 s** : surveillance — si le port 8080 meurt, relance automatique avec notification
- **Après une mise à jour** (`update.ps1`) : le bridge est relancé tout seul

## URL à donner à votre assistant IA

```
http://127.0.0.1:8080/mcp
```

## Questions fréquentes

**L'icône n'apparaît pas ?**
Le systray masque parfois les icônes : clic sur le chevron `^` près de l'horloge, glisse l'icône TopSolid MCP vers la barre visible.

**« Bridge refusera de démarrer en mode -Open » / clé API ?**
La clé est générée automatiquement à l'installation (affichée et copiée dans le presse-papiers). Perdue ? Clic droit sur l'icône → **Paramètres → Clé API...** : la clé actuelle s'affiche pré-remplie. Compromise ? **Paramètres → Régénérer la clé API** révoque l'ancienne et en copie une nouvelle dans le presse-papiers. Le bridge redémarre tout seul dans les deux cas.

**Comment tout supprimer ?**
Supprime le raccourci « TopSolidMCP Tray » dans `shell:startup`, quitte l'icône systray via « Quitter », et supprime le dossier d'installation.