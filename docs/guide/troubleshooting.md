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

## TopSolid est lance mais le serveur ne le voit pas

Verifier que :
1. L'acces distant est **active et TopSolid a ete redemarre** apres activation
2. Le port dans les options TopSolid correspond a celui que le serveur utilise (8090 par defaut)
3. Aucun pare-feu ne bloque le port 8090 en local
4. TopSolid est bien en cours d'execution (pas juste le lanceur)
