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

1. Aller sur la [page Releases](https://github.com/Julien38300/noemid-topsolid-automation/releases)
2. Telecharger `TopSolidMcpServer.zip` de la derniere version
3. Dezipper dans un dossier, par exemple `C:\TopSolidMCP\`

C'est tout. L'executable `TopSolidMcpServer.exe` est pret a l'emploi.

### Option B — Compiler depuis les sources (developpeurs)

```bash
git clone https://github.com/Julien38300/noemid-topsolid-automation.git
cd noemid-topsolid-automation/server
dotnet build TopSolidMcpServer.sln
```

L'executable sera dans `server/src/bin/Debug/net48/TopSolidMcpServer.exe`.

## Etape 3 — Configurer votre assistant IA

Choisissez votre client IA et ajoutez le serveur MCP :

### ChatGPT Desktop (Windows)

Dans ChatGPT Desktop : **Settings > Beta features > Model Context Protocol**

Ajoutez un serveur avec la commande :
```
C:\TopSolidMCP\TopSolidMcpServer.exe
```

### Claude Desktop

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

Tout client MCP compatible stdio fonctionne. Consultez le [guide d'integration complet](./integration) pour Claude Code, Antigravity, Windsurf, Continue, Hermes et plus.

## Etape 4 — Tester

Dans votre assistant IA, demandez :

> "Quelle est la designation de la piece ouverte dans TopSolid ?"

L'assistant utilisera `topsolid_run_recipe` avec la recette `lire_designation` et vous retournera la designation du document actif.

::: tip Connect() retourne false ?
C'est normal dans TopSolid v7.20. La connexion fonctionne quand meme. Verifiez que la version retournee est superieure a 0.
:::

## Ca ne marche pas ?

Consultez le [guide de depannage](./troubleshooting).
