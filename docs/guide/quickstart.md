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
2. Telecharger `TopSolidMcpServer.zip` de la derniere version
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

```powershell
cd C:\TopSolidMCP\bridge
npm install          # premiere fois uniquement
.\start-bridge.ps1   # demarre le bridge
```

Sortie attendue :
```
[bridge] HTTP endpoint : http://127.0.0.1:8080/mcp   <- copiez cette URL
[bridge] SSE (legacy)  : http://127.0.0.1:8080/sse
[bridge] Auth          : NONE -- local only
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

### claude.ai (web + app Windows)

claude.ai accepte uniquement des MCP distants via URL. Exposez le bridge avec un tunnel :

```powershell
# Dans un second terminal (bridge deja lance)
cloudflared tunnel --url http://127.0.0.1:8080
# -> https://<random>.trycloudflare.com
```

Dans claude.ai : **Settings → Connecteurs → Ajouter un connecteur personnalise** → `https://<random>.trycloudflare.com/mcp`

::: warning Securite
Ne laissez pas le tunnel ouvert sans surveillance. Voir le [guide complet du bridge](./bridge-http) pour securiser avec Cloudflare Access.
:::

## Etape 5 — Tester

Dans votre assistant IA, demandez :

> "Quelle est la designation de la piece ouverte dans TopSolid ?"

L'assistant doit appeler `topsolid_run_recipe` avec la recette `read_designation` et retourner la designation du document actif.

## Ca ne marche pas ?

Consultez le [guide de depannage](./troubleshooting).
