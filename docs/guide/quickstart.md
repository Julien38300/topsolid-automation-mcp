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

### Option A — Telecharger (recommande)

1. Aller sur la [page Releases](https://github.com/Julien38300/topsolid-automation-mcp/releases)
2. Telecharger `TopSolidMcpServer.zip` de la derniere version
3. Dezipper dans un dossier, par exemple `C:\TopSolidMCP\`

C'est tout. L'executable `TopSolidMcpServer.exe` est pret a l'emploi.

### Option B — Compiler depuis les sources (developpeurs)

```bash
git clone https://github.com/Julien38300/topsolid-automation-mcp.git
cd topsolid-automation-mcp/server
dotnet build TopSolidMcpServer.sln
```

L'executable sera dans `server/src/bin/Debug/net48/TopSolidMcpServer.exe`.

## Etape 3 — Configurer votre assistant IA

Choisissez votre client IA et ajoutez le serveur MCP :

::: warning Stdio local vs HTTP distant
`TopSolidMcpServer.exe` est un **MCP stdio local** — il tourne sur ta machine et communique par stdin/stdout. La plupart des clients de bureau (Claude Code CLI, Claude Desktop, VS Code, Cursor, Antigravity, OpenClaw) le supportent via un fichier de config.

En revanche, **claude.ai** (le site web + l'app Windows) accepte uniquement des **MCP distants en HTTP/SSE** via Settings → Connecteurs → "Ajouter un connecteur personnalise" (qui demande une URL + OAuth). Le TopSolid MCP n'est pas compatible avec ce canal sans un pont HTTP dedicace.

Même chose pour ChatGPT Desktop : pas de support stdio local (avril 2026, seuls les custom GPTs cloud).
:::

### Claude Code (CLI terminal)

```powershell
claude mcp add topsolid C:\TopSolidMCP\TopSolidMcpServer.exe
claude mcp list   # verification
```

### Claude Desktop (app Windows)

Editez le fichier `%APPDATA%\Claude\claude_desktop_config.json` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

Puis redemarrez Claude Desktop. Le serveur apparait avec son icone "prise" dans la barre de saisie.

### VS Code + GitHub Copilot

Dans VS Code, ouvrez les parametres (`Ctrl+,`), cherchez `mcp` et ajoutez dans `settings.json` :

```json
{
  "github.copilot.chat.mcp.servers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe"
    }
  }
}
```

### Cursor

Dans Cursor : **Settings > MCP Servers > Add Server**

- Name : `topsolid`
- Command : `C:\TopSolidMCP\TopSolidMcpServer.exe`
- Type : `stdio`

### Autres clients

Tout client MCP compatible stdio fonctionne. Consultez le [guide d'integration complet](./integration) pour Claude Code, Antigravity, Windsurf, Continue, OpenClaw et plus.

## Etape 4 — Tester

Dans votre assistant IA, demandez :

> "Quelle est la designation de la piece ouverte dans TopSolid ?"

L'assistant utilisera `topsolid_run_recipe` avec la recette `read_designation` et vous retournera la designation du document actif.

::: tip Connect() retourne false ?
C'est normal dans TopSolid v7.20. La connexion fonctionne quand meme. Verifiez que la version retournee est superieure a 0.
:::

## Ca ne marche pas ?

Consultez le [guide de depannage](./troubleshooting).
