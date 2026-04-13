# Demarrage rapide

## Prerequis

- **TopSolid 7.15+** installe et lance
- **.NET Framework 4.8** (inclus dans Windows 10+)
- Un client MCP compatible (Claude Code, Antigravity, Claude Desktop, Hermes, ou tout client MCP stdio)

## Etape 1 — Activer l'acces distant dans TopSolid

Dans TopSolid, aller dans **Outils > Options > General** puis descendre jusqu'a la section **Automation** (tout en bas) :

1. Cocher **"Gerer l'acces distant"**
2. Verifier que le numero de port est **8090** (valeur par defaut)
3. Cliquer sur la coche verte pour valider
4. **Redemarrer TopSolid** (obligatoire — le message l'indique)

::: warning Prerequis obligatoire
Sans cette option activee, le serveur MCP ne pourra pas se connecter a TopSolid. C'est la cause n1 des problemes de connexion.
:::

## Etape 2 — Compiler le serveur MCP

```bash
git clone https://github.com/Julien38300/noemid-topsolid-automation.git
cd noemid-topsolid-automation/server
dotnet build TopSolidMcpServer.sln
```

L'executable sera dans `server/src/bin/Debug/net48/TopSolidMcpServer.exe`.

## Etape 3 — Tester la connexion

Ouvrez un terminal et lancez :

```bash
echo {"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"topsolid_get_state","arguments":{}}} | chemin\vers\TopSolidMcpServer.exe
```

Si TopSolid est lance avec l'acces distant active, vous devriez voir une reponse contenant `"connected": true` et un numero de version.

::: tip Connect() retourne false ?
C'est normal dans TopSolid v7.20. La connexion fonctionne quand meme. Verifiez que la version retournee est superieure a 0.
:::

## Etape 4 — Premiere recette

```bash
echo {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"topsolid_run_recipe","arguments":{"recipe_name":"lire_designation"}}} | chemin\vers\TopSolidMcpServer.exe
```

Cette commande retourne la designation du document actuellement ouvert dans TopSolid.

## Etape suivante

Configurez votre client MCP pour utiliser le serveur automatiquement : [Guide d'integration](./integration)
