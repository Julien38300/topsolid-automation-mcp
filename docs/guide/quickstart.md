# Demarrage rapide

## Prerequis

- **TopSolid 7.15+** installe et lance
- **Windows 10+** (.NET Framework 4.8 inclus)

## La voie noob — tout en double-clic (v1.8.0+)

Depuis la v1.8.0, le zip de release contient un dossier `install/` qui rend le serveur natif HTTP accessible sans aucune ligne de commande, sans Node.js :

1. **Dezipper** la release, par exemple dans `C:\TopSolidMCP\`
2. **Double-clic** sur `install\Installer_TopSolidMCP.bat`
3. **Suivre la fenetre** : la cle API se genere toute seule (chiffree DPAPI dans `settings.json`, copiee dans le presse-papiers), la tache planifiee de demarrage automatique s'enregistre toute seule, les restes d'une ancienne installation bridge v1.7 sont nettoyés

Un processus unique tourne : le serveur natif (stdio + endpoint HTTP sur le port 8080). L'icône près de l'horloge (systray) en est le reflet. Clic droit pour Statut / Redemarrer / Arreter / Clé API. Details dans [`install/README.md`](https://github.com/Julien38300/topsolid-automation-mcp/blob/main/install/README.md).

::: tip v1.8.0+ — tray enrichi
Depuis la **v1.8.0**, l'icône tray gère aussi :

- **Clé API** — générer / copier / régénérer / révoquer la clé `X-API-Key` de l'endpoint HTTP natif (stockée chiffrée DPAPI dans `settings.json`, jamais en clair).
- **Connexion automatique** — le serveur se reconnecte seul à TopSolid s'il a été lancé avant TopSolid ou si TopSolid est relancé (backoff 15 s → 30 s → 60 s plafonné). Activable/désactivable depuis le menu, état mémorisé.
- **Nouveautés** — ouvre directement la page GitHub de la **release installée** (corrections, évolutions). Le bouton *Vérifier les mises à jour* ouvre maintenant automatiquement cette page au lancement de la mise à jour.
- **Signaler un bug / Suggérer une évolution** — ouvre le formulaire GitHub pré-rempli (template bug/feature, version serveur, OS et état de connexion injectés automatiquement ; la clé API est **scrubbée** de tous les champs).
:::

L'URL a donner a votre assistant IA reste la meme : `http://127.0.0.1:8080/mcp`.

::: details La voie manuelle (developpeurs / serveurs sans session graphique)
Suivre les etapes ci-dessous pour demarrer le serveur a la main.
:::

## Etape 1 — Activer l'acces distant dans TopSolid

Dans TopSolid, aller dans **Outils > Options > General** puis descendre jusqu'a la section **Automation** (tout en bas) :

1. Cocher **"Gerer l'acces distant"**
2. Verifier que le numero de port est **8090** (valeur par defaut)
3. Cliquer sur la coche verte pour valider
4. **Redemarrer TopSolid** (obligatoire — le message l'indique)

::: warning Prerequis obligatoire
Sans cette option activee, le serveur MCP ne pourra pas se connecter a TopSolid. C'est la cause numero 1 des problemes de connexion.
:::

## Etape 2 — Telecharger le serveur MCP

1. Aller sur la [page Releases](https://github.com/Julien38300/topsolid-automation-mcp/releases)
2. Telecharger `TopSolidMcpServer-vX.Y.Z.zip` de la derniere version
3. Dezipper dans un dossier, par exemple `C:\TopSolidMCP\`

C'est tout. L'executable `TopSolidMcpServer.exe` est pret a l'emploi.

::: details Compiler depuis les sources (developpeurs)
```bash
git clone https://github.com/Julien38300/topsolid-automation-mcp.git
cd topsolid-automation-mcp/server
dotnet build TopSolidMcpServer.sln
```
L'executable sera dans `server/src/bin/Debug/net48/TopSolidMcpServer.exe`.
:::

## Etape 3 — Demarrer le serveur (HTTP natif recommande)

::: tip Pourquoi le serveur HTTP natif ?
Depuis la v1.8.0, l'executable embarque lui-meme l'endpoint HTTP : **un seul process** sert a la fois stdio (client configure en `command`) et HTTP natif (clients configures en `url`). Tous vos clients IA HTTP s'y connectent via une simple URL — sans chemin vers l'exe, sans redemarrer les clients quand le serveur change, et avec la possibilite d'ouvrir l'endpoint a claude.ai web ou mobile via un tunnel authentifie.

En mode stdio seul, chaque client IA relance un processus `TopSolidMcpServer.exe` separement. Ca marche, mais c'est plus lourd a configurer et le serveur singleton bloque le deuxieme client.
:::

**Aucun prerequis supplementaire** — pas de Node.js, tout est dans l'executable.

::: tip Le serveur natif est dans le zip depuis la v1.8.0
Les etapes ci-dessous restent utiles pour les developpeurs et les serveurs sans session graphique. Pour l'experience complete sans terminal, utilise le dossier `install/` du zip (voir la voie noob en haut de cette page).
:::

```powershell
cd C:\TopSolidMCP
.\TopSolidMcpServer.exe --http-standalone
```

Sortie attendue :
```
[MCP-INFO] Native HTTP MCP endpoint started on port 8080
[MCP-INFO] http-standalone mode: the process persists after stdin EOF while the HTTP listener is alive
[MCP-INFO] API key: read from settings.json (DPAPI) / TOPSOLID_MCP_API_KEY / auto-generated
```

Laissez ce terminal ouvert. Le serveur tourne tant que la fenetre est ouverte ; l'installer cree la tache planifiee qui fait la meme chose sans console.

## Etape 4 — Configurer votre assistant IA

### Mode HTTP natif (recommande) — une URL pour tous

Une fois le serveur demarre (ou via l'installer), la config est identique pour tous les clients :

::: code-group
```json [Claude Desktop]
// %APPDATA%\Claude\claude_desktop_config.json
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```powershell [Claude Code CLI]
claude mcp add --transport http topsolid http://127.0.0.1:8080/mcp
```
```json [Cursor / Windsurf]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [VS Code + Copilot]
// .vscode/mcp.json
{
  "servers": {
    "topsolid": {
      "type": "http",
      "url": "http://127.0.0.1:8080/mcp"
    }
  }
}
```
```json [Antigravity]
{
  "mcpServers": {
    "topsolid": {
      "url": "http://127.0.0.1:8080/mcp",
      "disabled": false
    }
  }
}
```
:::

### Mode stdio (alternatif) — un client a la fois

Si vous preferez piloter le serveur depuis un seul client (sans HTTP) :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

::: warning Limite du mode stdio
Le serveur est un singleton — un seul client IA peut l'utiliser a la fois. Le deuxieme client recevra `TopSolidMcpServer is already running`.
:::

---

## Options de lancement et variables

Toutes ces options se passent soit en argument de ligne de commande, soit par variable d'environnement (pratique dans un fichier de configuration MCP, qui accepte un bloc `env`).

| Option | Variable | Effet |
|---|---|---|
| `--read-only` | `TOPSOLID_MCP_READ_ONLY=1` | `topsolid_modify_script` n'est pas enregistre ; les recettes tournent en lecture seule |
| `--no-tray` | `TOPSOLID_MCP_NO_TRAY=1` | Pas d'icone dans la zone de notification (serveur headless, session 0, CI) |
| `--port <n>` | — | Port Automation de TopSolid (defaut 8090) |
| `--compile <fichier>` | — | Compile un fichier sans l'executer, puis sort (code 0 = OK) |
| `--version` / `-v` | — | Affiche la version et sort |
| — | `TOPSOLID_BIN_PATH` | Dossier contenant `TopSolid.Kernel.Automating.dll`, quand la detection automatique echoue |
| — | `TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC` | Delai max d'execution d'un script, en secondes (defaut 60) |
| — | `TOPSOLID_MCP_ALLOW_UNSAFE=1` | Desactive le controle des APIs interdites — debug uniquement, jamais en usage courant |

Exemple de configuration en lecture seule, sans icone de notification, avec un chemin TopSolid force :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "args": ["--read-only", "--no-tray"],
      "env": {
        "TOPSOLID_BIN_PATH": "C:\\Missler\\V627\\bin",
        "TOPSOLID_MCP_SCRIPT_TIMEOUT_SEC": "120"
      }
    }
  }
}
```

::: danger N'auto-approuvez pas les outils de script
`topsolid_execute_script` et `topsolid_modify_script` compilent le C# recu et l'executent **en pleine confiance dans le processus du serveur**, sur votre poste, avec `System.IO` disponible. « Lecture seule » veut dire « hors transaction de modification TopSolid » — ce n'est pas un bac a sable.

Ne les mettez pas dans la liste des outils toujours autorises de votre client MCP, et relisez chaque script avant de le laisser tourner. Si vous n'avez besoin que de consulter, `--read-only` supprime purement et simplement `topsolid_modify_script`.
:::

---

### claude.ai (web + app Windows)

claude.ai accepte uniquement des MCP distants via URL. Il faut donc exposer le serveur via un tunnel — et l'exposer **authentifie**.

::: danger Le tunnel donne un acces distant a votre machine
Publier l'endpoint, c'est publier `topsolid_execute_script` et `topsolid_modify_script`, qui executent du code arbitraire sur votre poste. Un tunnel nu `trycloudflare.com`, sans authentification, suffit a quiconque connait l'URL.

Passez par un **tunnel nomme derriere Cloudflare Access** — procedure detaillee dans le [guide HTTP](./bridge-http#solution-recommandee-cloudflare-access-gratuit). Le tunnel nu n'est pas recommande.
:::

Une fois l'application Cloudflare Access en place, dans claude.ai : **Settings → Connecteurs → Ajouter un connecteur personnalise** → `https://topsolid-mcp.votredomaine.com/mcp`

## Etape 5 — Tester

Dans votre assistant IA, demandez :

> "Quelle est la designation de la piece ouverte dans TopSolid ?"

L'assistant doit appeler `topsolid_run_recipe` avec la recette `read_designation` et retourner la designation du document actif.

## Ca ne marche pas ?

Consultez le [guide de depannage](./troubleshooting).
