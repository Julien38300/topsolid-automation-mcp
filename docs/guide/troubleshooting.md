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

## Erreurs de compilation de scripts

Le serveur compile du **C# 5** (.NET Framework 4.8). Les syntaxes C# 6+ ne sont pas supportees :

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

## Mise a jour du serveur

Le serveur inclut un script de mise a jour automatique :

```powershell
.\update.ps1
```

Le script compare la version locale avec la derniere release GitHub et propose la mise a jour si une nouvelle version est disponible.
