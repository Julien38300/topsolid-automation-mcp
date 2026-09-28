# Installation TopSolid MCP — guide noob

**Tout se fait en double-clic. Aucune ligne de commande à taper.**

## Installation en 3 clics

1. **Double-clic sur `Installer_TopSolidMCP.bat`**
   - Installe Node.js automatiquement si absent (via winget)
   - Installe les composants du bridge
   - Demande la clé API dans une petite fenêtre graphique
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
Le mode `-Open` (accessible depuis le réseau) exige une clé. L'installeur la demande dans une fenêtre graphique ; si tu l'as perdue, relance l'installeur ou saisis-la via les propriétés système → variables d'environnement → `TOPSOLID_MCP_API_KEY`.

**Comment tout supprimer ?**
Supprime le raccourci « TopSolidMCP Tray » dans `shell:startup`, quitte l'icône systray via « Quitter », et supprime le dossier d'installation.