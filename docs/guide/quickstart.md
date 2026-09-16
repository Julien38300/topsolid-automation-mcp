# Demarrage rapide

## Prerequis

- **TopSolid 7.15+** installe et lance
- **Windows 10+** (.NET Framework 4.8 inclus)

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

## Etape 3 — Demarrer le bridge HTTP/SSE (recommande)

::: tip Pourquoi le bridge ?
Le bridge lance le serveur **une seule fois** et l'expose comme un endpoint HTTP local. Tous vos clients IA s'y connectent via une simple URL — sans chemin vers l'exe, sans redemarrer les clients quand le serveur change, et avec la possibilite d'ouvrir le bridge a claude.ai web ou mobile via un tunnel.

Sans bridge (mode stdio), chaque client IA relance un processus `TopSolidMcpServer.exe` separement. Ca marche, mais c'est plus lourd a configurer et le serveur singleton bloque le deuxieme client.
:::

**Prerequis : Node.js 18+** ([nodejs.org](https://nodejs.org/))

::: warning Le dossier `bridge/` n'est pas dans le zip de release
Le zip ne contient que le serveur, ses DLL et `data/`. Recuperez `bridge/` depuis le depot (clone ou telechargement du code source) et placez-le ou vous voulez — `start-bridge.ps1` trouve l'exe via `C:\TopSolidMCP\TopSolidMcpServer.exe`, le build local du depot, ou la variable `TOPSOLID_MCP_EXE`.
:::

```powershell
cd <depot>\bridge
npm install          # premiere fois uniquement
.\start-bridge.ps1   # demarre le bridge
```

Sortie attendue :
```
[bridge] stdio server: C:\TopSolidMCP\TopSolidMcpServer.exe
[bridge] HTTP endpoint : http://127.0.0.1:8080/mcp   <- copiez cette URL
[bridge] SSE (legacy)  : http://127.0.0.1:8080/sse
[bridge] Auth          : NONE -- 127.0.0.1 bind only.
```

Laissez ce terminal ouvert. Le bridge tourne tant que la fenetre est ouverte.

## Etape 4 — Configurer votre assistant IA

### Mode bridge (recommande) — une URL pour tous

Une fois le bridge demarre, la config est identique pour tous les clients :

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

### Mode stdio (alternatif) — sans bridge, un client a la fois

Si vous ne voulez pas utiliser le bridge ou n'avez pas Node.js :

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

claude.ai accepte uniquement des MCP distants via URL. Il faut donc exposer le bridge — et l'exposer **authentifie**.

::: danger Le pont donne un acces distant a votre machine
Publier le bridge, c'est publier `topsolid_execute_script` et `topsolid_modify_script`, qui executent du code arbitraire sur votre poste. Un tunnel nu `trycloudflare.com`, sans authentification, suffit a quiconque connait l'URL.

Passez par un **tunnel nomme derriere Cloudflare Access** — procedure detaillee dans le [guide du bridge](./bridge-http#solution-recommandee-cloudflare-access-gratuit). Le tunnel nu n'est pas recommande.
:::

Une fois l'application Cloudflare Access en place, dans claude.ai : **Settings → Connecteurs → Ajouter un connecteur personnalise** → `https://topsolid-mcp.votredomaine.com/mcp`

## Etape 5 — Tester

Dans votre assistant IA, demandez :

> "Quelle est la designation de la piece ouverte dans TopSolid ?"

L'assistant doit appeler `topsolid_run_recipe` avec la recette `read_designation` et retourner la designation du document actif.

## Ca ne marche pas ?

Consultez le [guide de depannage](./troubleshooting).
