# Depannage

## Le serveur ne se connecte pas a TopSolid

**Cause la plus frequente** : l'acces distant n'est pas active dans TopSolid.

1. Dans TopSolid : **Outils > Options > General > Automation**
2. Cocher **"Gerer l'acces distant"**
3. Verifier que le port est **8090**
4. **Redemarrer TopSolid** (obligatoire apres ce changement)

## "Connect() retourne false"

C'est un comportement normal dans TopSolid v7.20 — bug connu. La connexion fonctionne quand meme.

**Verification** : si `topsolid_get_state` retourne une version superieure a 0, la connexion est OK. Ne pas se fier au booleen `Connect()`.

## Port 8090 occupe

```bash
netstat -ano | findstr 8090
```

Si un autre processus utilise le port, soit le fermer, soit changer le port dans les options TopSolid (Outils > Options > General > Automation > Numero de port).

## Plusieurs instances du serveur MCP

Le serveur utilise un Mutex nomme — une seule instance peut tourner a la fois. Si le serveur refuse de demarrer :

```bash
tasklist | findstr TopSolidMcpServer
taskkill /F /IM TopSolidMcpServer.exe
```

## Outils d'ecriture/execution echouent avec "DLL not found"

**Symptome** : `topsolid_compile`, `topsolid_run_recipe`, `topsolid_execute_script` ou `topsolid_modify_script` retournent :

```
Error: TopSolid DLL not found at C:\Program Files\TOPSOLID\TopSolid 7.21\bin\...
```

**Cause** : le serveur n'a pas trouve votre installation TopSolid automatiquement.

**Resolution (v1.6.8+)** : le serveur detecte automatiquement la version installee via le registre Windows. Si cela echoue encore, definissez la variable d'environnement `TOPSOLID_BIN_PATH` :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "env": {
        "TOPSOLID_BIN_PATH": "C:\\Program Files\\TOPSOLID\\TopSolid 7.20\\bin"
      }
    }
  }
}
```

Remplacez `7.20` par votre version reelle (visible dans **Aide > A propos** dans TopSolid). Sur une installation historique Missler, le chemin ressemble plutot a `C:\Missler\V627\bin`.

La liste complete des drapeaux et variables d'environnement est dans le [demarrage rapide](./quickstart#options-de-lancement-et-variables).

**Resolution alternative (toutes versions)** : creer une jonction de repertoire en PowerShell admin :

```powershell
New-Item -ItemType Junction `
  -Path "C:\Program Files\TOPSOLID\TopSolid 7.21" `
  -Target "C:\Program Files\TOPSOLID\TopSolid 7.20"
```

## Installation TopSolid partielle (Design ou Drafting absent)

Toutes les licences TopSolid n'installent pas les memes modules Automation. Seul `TopSolid.Kernel.Automating.dll` est obligatoire ; `TopSolid.Cad.Design.Automating.dll` et `TopSolid.Cad.Drafting.Automating.dll` sont optionnels.

**Avant le correctif de l'[issue #11](https://github.com/Julien38300/topsolid-automation-mcp/issues/11)**, l'absence d'un de ces deux modules faisait echouer la connexion et le serveur tombait au demarrage.

**Depuis ce correctif**, les deux modules sont **detectes a l'execution**, au moment de la connexion. Un module absent n'est plus une erreur fatale : le serveur le signale sur `stderr` et dans `topsolid_get_state`, ne reference plus l'assembly correspondante a la compilation des scripts, et continue a servir tout le reste (recettes PDM, parametres, masse/volume, base de connaissance...).

Le module detecte est visible dans la reponse de `topsolid_get_state` :

```
Modules : Design = available, Drafting = not available
```

et dans les logs au demarrage :

```
[TopSolidConnector] Connected to TopSolid v<version> on port 8090 (Design: yes, Drafting: no).
```

**Cas reel rencontre** : TopSolid 2026 (V627) installe sous `C:\Missler\V627\bin` — c'est-a-dire selon l'ancienne arborescence Missler `V<nnn>`, pas `C:\Program Files\TOPSOLID\TopSolid 7.xx` — avec une licence **Design / Wood / Fold mais sans Drafting**. Sur ce poste, le serveur demarre, se connecte et sert normalement les recettes qui ne dependent pas de Drafting.

::: warning Ce que renvoient les recettes qui dependent d'un module absent
Le namespace du module manquant n'est pas importe dans les scripts generes (sinon **tous** les scripts echoueraient a compiler). Une recette de mise en plan lancee sans le module Drafting echoue donc **a la compilation**, avec une erreur du type :

```
Compilation error:
Line 3: CS0103 - Le nom 'TopSolidDraftingHost' n'existe pas dans le contexte actuel
```

C'est un echec propre, renvoye au client MCP — le serveur reste debout. Il n'y a aujourd'hui **pas** de desenregistrement automatique des outils ni de message « module non disponible » par recette : verifiez la ligne `Modules :` de `topsolid_get_state` avant de conclure a un bug.
:::

::: tip Si la detection du dossier d'installation echoue
Le resolveur cherche dans le registre (`Missler Software`, sous-cles `TopSolid 7.xx` **et** `V<nnn>`), puis dans Program Files, puis sur les disques fixes. S'il ne trouve rien, il le dit clairement sur `stderr` et vous pouvez pointer le dossier a la main :

```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "env": { "TOPSOLID_BIN_PATH": "C:\\Missler\\V627\\bin" }
    }
  }
}
```

`TOPSOLID_BIN_PATH` doit pointer sur le dossier qui contient `TopSolid.Kernel.Automating.dll`.
:::

## Erreurs de compilation de scripts

Le serveur compile les scripts via `CSharpCodeProvider` — le compilateur `csc` livre avec le .NET Framework, **pas Roslyn**. Le code est donc compile en **C# 5** et les syntaxes C# 6+ ne sont pas supportees :

| Syntaxe interdite | Alternative C# 5 |
|-------------------|-------------------|
| `$"Hello {name}"` | `string.Format("Hello {0}", name)` |
| `obj?.Method()` | `if (obj != null) obj.Method()` |
| `var (a, b) = ...` | Declarations separees |
| `using var x = ...` | `using (var x = ...) { }` |
| `nameof(x)` | `"x"` (chaine en dur) |

## Encodage / accents corrompus

Le serveur utilise UTF-8. Si les accents sont corrompus dans les reponses, verifier que le client MCP envoie bien les requetes en UTF-8 (pas Windows-1252 ou ISO-8859-1).

## Plusieurs versions de TopSolid ouvertes simultanement

Si vous avez plusieurs instances TopSolid (ex: v7.17 et v7.20), le serveur doit cibler la bonne avec `--port` :

```bash
TopSolidMcpServer.exe --port 8090
```

Chaque instance TopSolid a son propre port dans **Outils > Options > General > Automation**. Verifiez quel port est configure dans l'instance que vous voulez piloter.

Configuration MCP avec port specifique :
```json
{
  "mcpServers": {
    "topsolid": {
      "command": "C:\\TopSolidMCP\\TopSolidMcpServer.exe",
      "args": ["--port", "8090"]
    }
  }
}
```

::: warning Sans --port
Sans `--port`, le serveur utilise 8090 par defaut. Si l'instance TopSolid sur le port 8090 n'est pas celle que vous voulez piloter, vous aurez un resultat inattendu (mauvais document, "aucun document en edition").
:::

## "Aucun document en edition" alors qu'un document est ouvert

Causes possibles :
1. **Plusieurs instances TopSolid** — le serveur est connecte a la mauvaise instance (voir section ci-dessus)
2. **Le document n'est pas en edition** — il est ouvert mais pas en mode edition (double-cliquez dessus dans l'arborescence)
3. **TopSolid vient d'etre redemarre** — relancez le client MCP pour forcer une reconnexion

## TopSolid est lance mais le serveur ne le voit pas

Verifier que :
1. L'acces distant est **active et TopSolid a ete redemarre** apres activation
2. Le port dans les options TopSolid correspond a celui que le serveur utilise (8090 par defaut)
3. Aucun pare-feu ne bloque le port 8090 en local
4. TopSolid est bien en cours d'execution (pas juste le lanceur)

## Le bridge ne demarre pas : `MODULE_NOT_FOUND` / `mcp-proxy` introuvable

Si `start-bridge.ps1` ou un `.bat` qui lance `npx mcp-proxy` echoue avec une erreur Node du type `Cannot find module '...mcp-proxy\dist\cli.js'` :

1. **Une installation npm corrompue** — `bridge/node_modules/mcp-proxy/dist` peut ne contenir qu'un fichier vide apres une install interrompue. Reinstalle proprement :
   ```powershell
   cd bridge
   Remove-Item -Recurse -Force node_modules, package-lock.json
   npm install
   ```
2. **Le point d'entree de mcp-proxy a change** — depuis mcp-proxy 6.x, le binaire reel est `dist/bin/mcp-proxy.mjs`, pas `dist/cli.js`. `start-bridge.ps1` passe par `npx`, qui lit `node_modules/.bin/mcp-proxy` et trouve le bon point d'entree tout seul — c'est un `.bat` maison qui code `dist\cli.js` en dur qui casse. **Utilise `start-bridge.ps1`, pas un wrapper `npx`/`node` ecrit a la main.**
3. **`npx` telecharge une version non pinnee** — si `bridge/node_modules/mcp-proxy` manque, `npx` telecharge la derniere version au lieu du 6.4.6 pinne dans `package-lock.json`. `start-bridge.ps1` refuse alors de demarrer avec une cle d'API (fail-closed, voir [bridge/README.md](https://github.com/Julien38300/topsolid-automation-mcp/blob/main/bridge/README.md)) — lance `npm install` dans `bridge/` d'abord.

## Mise a jour du serveur

Le serveur inclut un script de mise a jour automatique :

```powershell
.\update.ps1
```

Le script compare la version locale avec la derniere release GitHub et propose la mise a jour si une nouvelle version est disponible.
