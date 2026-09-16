# Bridge HTTP/SSE pour claude.ai (et autres clients web)

Le `TopSolidMcpServer.exe` est un serveur MCP **stdio local** — il communique par stdin/stdout et vit dans le même processus que son client (Claude Desktop, Claude Code CLI, etc.). Cela ne marche pas pour les clients **web** (claude.ai, ChatGPT, apps mobiles) qui n'ont aucun moyen d'exécuter un .exe sur ta machine.

Ce guide installe un **pont HTTP/SSE** qui enveloppe le serveur stdio dans un endpoint HTTP. Les clients distants s'y connectent par URL.

::: danger A lire avant de commencer
Le pont expose **des outils d'execution de code arbitraire sur votre poste**. `topsolid_execute_script` et `topsolid_modify_script` compilent le C# recu et l'executent en pleine confiance dans le processus du serveur, avec `System.IO` disponible ; les recettes d'ecriture modifient votre PDM. « Lecture seule » signifie ici « hors transaction de modification TopSolid », pas « bac a sable ».

Consequence directe : **un pont accessible depuis Internet sans authentification est un acces distant a votre machine.** Mettez [Cloudflare Access](#solution-recommandee-cloudflare-access-gratuit) devant le tunnel avant de donner l'URL a quoi que ce soit. Et si vous n'avez besoin que de consulter, lancez le serveur avec `--read-only` : `topsolid_modify_script` n'est alors pas enregistre du tout.
:::

## Architecture

```
┌────────────────────────┐         HTTPS         ┌────────────────────┐
│ claude.ai (cloud web)  │ ─────────────────────>│ Cloudflare Access  │
└────────────────────────┘                       │ + tunnel nomme     │
                                                 │ (auth obligatoire) │
                                                 └─────────┬──────────┘
                                                           │ local 127.0.0.1:8080
                                                 ┌─────────v──────────┐
                                                 │ bridge (mcp-proxy) │   Streamable HTTP + SSE
                                                 │ localhost only     │
                                                 └─────────┬──────────┘
                                                           │ stdio JSON-RPC
                                                 ┌─────────v──────────┐
                                                 │ TopSolidMcpServer  │
                                                 │ .exe (net48)       │
                                                 └─────────┬──────────┘
                                                           │ WCF/TCP 8090
                                                 ┌─────────v──────────┐
                                                 │ TopSolid 7         │
                                                 └────────────────────┘
```

Le pont traduit du **Streamable HTTP** (spec MCP 2025-03-26) vers **stdio** et inversement. Il utilise [`mcp-proxy`](https://github.com/punkpeye/mcp-proxy), maintenu par l'écosystème, pas de code custom de notre côté.

## Prérequis

- **Node.js 18+** (pour `npx` et `mcp-proxy`)
- **TopSolidMcpServer.exe** buildé (soit dans `server/src/bin/Release/net48/`, soit à `C:\TopSolidMCP\TopSolidMcpServer.exe`)
- **Cloudflared** (gratuit) pour exposer le bridge publiquement — claude.ai est dans le cloud et ne peut pas joindre `localhost` — **avec Cloudflare Access devant** (voir [Sécurité](#securite-ce-que-tu-exposes-reellement))

Installation cloudflared (Windows) :
```powershell
winget install --id Cloudflare.cloudflared
```

## Démarrer le bridge (local uniquement)

```powershell
cd bridge
npm install            # première fois
.\start-bridge.ps1
```

Sortie attendue :
```
[bridge] stdio server: C:\Users\...\TopSolidMcpServer.exe
[bridge] HTTP endpoint : http://127.0.0.1:8080/mcp
[bridge] SSE (legacy)  : http://127.0.0.1:8080/sse
[bridge] Auth          : NONE -- 127.0.0.1 bind only.
[bridge]                  A local bind is not a security boundary: any page your
[bridge]                  browser loads can POST to 127.0.0.1 unless the Origin
[bridge]                  header is validated. See bridge/README.md.
```

Test rapide :
```powershell
curl -X POST http://127.0.0.1:8080/mcp `
  -H "Accept: application/json, text/event-stream" `
  -H "Content-Type: application/json" `
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"smoke","version":"0.1"}}}'
```

Si tu vois un `event: message` + la réponse JSON-RPC d'`initialize`, le bridge fonctionne.

## Sécurité — ce que tu exposes réellement

Un pont MCP public n'est pas « une API de lecture CAO ». Sans authentification, quiconque connaît l'URL peut :

- **exécuter du code C# arbitraire sur ta machine** via `topsolid_execute_script` / `topsolid_modify_script` : le code est compilé puis exécuté en pleine confiance dans le processus du serveur, avec `System.IO` disponible — donc lecture et écriture de fichiers partout où ton compte Windows le peut ;
- renommer tes pièces, modifier tes paramètres, supprimer des occurrences ;
- exporter ton PDM vers une destination hostile ;
- déclencher des commandes TopSolid arbitraires.

La liste noire de `ScriptExecutor` (`System.Diagnostics.Process`, `System.Net`, `System.Reflection`, `DllImport`, `File.Delete`, `Directory.Delete`, registre, `Environment.Exit`) est un garde-fou contre un modèle qui dérape, **pas** une frontière de sécurité — et `TOPSOLID_MCP_ALLOW_UNSAFE=1` la supprime.

Deux réflexes avant d'ouvrir le pont :

1. **Authentifier le tunnel** (section suivante) ;
2. lancer le serveur avec **`--read-only`** (ou `TOPSOLID_MCP_READ_ONLY=1`) si tu n'as besoin que de consulter : `topsolid_modify_script` n'est alors pas enregistré du tout et les recettes tournent en lecture seule.

### Le problème avec claude.ai + auth

claude.ai accepte deux modes d'auth côté UI :
- **Aucun** (anonyme)
- **OAuth 2.1** (Client ID + Client Secret) — nécessite un serveur OAuth complet côté bridge, lourd

**Ce qui n'est PAS supporté** : Bearer token statique, `X-API-Key`, HTTP Basic. L'option `--apiKey` de `mcp-proxy` existe mais claude.ai ne peut pas l'envoyer ([issue claude-ai-mcp#112](https://github.com/anthropics/claude-ai-mcp/issues/112)).

D'où la solution ci-dessous : garder le bridge authless côté local, et mettre l'authentification **devant** le tunnel.

## Solution recommandée : Cloudflare Access (gratuit)

**C'est la seule configuration recommandée pour exposer le pont.** CF Access garde l'URL derrière une politique par email : seul ton email peut obtenir un token, ajouté automatiquement via cookies côté navigateur ou JWT côté API.

Setup (résumé, voir les docs Cloudflare pour le détail) :

1. Créer un compte **Cloudflare Zero Trust** (free tier : 50 users)
2. Créer un tunnel **nommé** attaché à ton domaine : `cloudflared tunnel create topsolid-mcp` + route DNS
3. Créer une **Application Access** sur `topsolid-mcp.tondomaine.com` → Policy : `include: emails = toi@example.com`
4. Dans claude.ai, donner l'URL `https://topsolid-mcp.tondomaine.com/mcp` (noter le `/mcp`). CF intercepte, force l'auth, le backend Anthropic s'authentifie via une **service token** CF (à configurer en "bypass for CF tokens")

C'est la voie officielle pour ce use case et ça reste 100 % gratuit.

### Validation de l'en-tête `Origin`

La spécification MCP exige qu'un serveur HTTP local **valide l'en-tête `Origin` de chaque requête entrante** et rejette celles qui viennent d'une origine inattendue, précisément pour bloquer les attaques DNS-rebinding qui transforment une page web visitée par l'utilisateur en client de ton serveur local. Elle recommande également de ne binder que sur `127.0.0.1` — ce que fait `start-bridge.ps1` par défaut.

Le pont est un `mcp-proxy` non modifié : **rien dans cette configuration ne garantit cette validation.** Ne compte donc pas dessus. Le bind sur `127.0.0.1` plus l'authentification en amont (Cloudflare Access) sont les protections sur lesquelles tu peux réellement t'appuyer.

### Clé d'API du pont

`mcp-proxy` accepte une clé (`--apiKey`). `start-bridge.ps1` la transmet **par l'environnement**, jamais sur la ligne de commande — les arguments de processus sont lisibles par n'importe quel utilisateur local et finissent dans les journaux d'audit :

```powershell
$env:TOPSOLID_MCP_API_KEY = "<cle>"
.\start-bridge.ps1 -RequireApiKey     # refuse de demarrer sans cle
```

`.\start-bridge.ps1 -Open` (bind `0.0.0.0`, donc tout le LAN) **exige** une clé et refuse de démarrer sans. Cela reste déconseillé : préfère le bind local par défaut derrière un tunnel authentifié. Et rappelle-toi que claude.ai ne sait pas envoyer cette clé — elle protège les clients que tu contrôles, pas le connecteur claude.ai.

## Tunnel nu (`trycloudflare.com`) — non recommandé

```powershell
cloudflared tunnel --url http://127.0.0.1:8080
```

Sortie :
```
+--------------------------------------------------------------------------------------------+
|  Your quick Tunnel has been created! Visit it at (it may take some time to be reachable):  |
|  https://random-words.trycloudflare.com                                                    |
+--------------------------------------------------------------------------------------------+
```

::: danger Pourquoi ce n'est pas recommandé
Cette URL est **publique et sans aucune authentification**. Elle donne à quiconque la découvre l'exécution de code arbitraire sur ton poste (voir la section Sécurité ci-dessus). L'obscurité du nom aléatoire n'est pas une protection : les domaines `trycloudflare.com` sont scannés.

Si tu l'utilises malgré tout, pour un POC strictement local et court :
- ne partage JAMAIS l'URL ;
- lance le serveur en `--read-only` ;
- coupe le tunnel dès que tu ne l'utilises plus (`Ctrl+C`) ;
- n'utilise pas `.\start-bridge.ps1 -Open` (bind `0.0.0.0`) : il exige une clé API, mais un listener LAN reste une surface inutile ici.
:::

## Brancher claude.ai

1. Ouvrir **claude.ai** → **Settings** → **Connecteurs** → **Ajouter un connecteur personnalisé**
2. **URL du serveur MCP distant** : `https://topsolid-mcp.tondomaine.com/mcp` (l'URL protégée par CF Access)
3. **Ajouter**

claude.ai envoie un `initialize` puis `tools/list` — tu dois voir apparaître les **13 outils TopSolid** dans l'UI du connecteur (12 si le serveur tourne en `--read-only`).

## Spec MCP utilisée

Le bridge implémente **Streamable HTTP** (MCP spec `2025-03-26`) :
- `POST /mcp` : requête/réponse JSON-RPC, réponse en `text/event-stream` (SSE) ou `application/json`
- `GET /mcp` : canal SSE passif pour les notifications serveur → client
- `DELETE /mcp` : termine la session (header `Mcp-Session-Id`)
- Header `Mcp-Session-Id` défini sur `InitializeResult`, écho obligatoire sur requêtes suivantes

Le bridge expose également l'ancien transport **HTTP+SSE** (`2024-11-05`) sur `/sse` + `/messages` pour compat clients legacy (hors claude.ai, qui est à jour).

Côté serveur, la négociation de révision à l'`initialize` accepte **2025-06-18**, **2025-03-26** et **2024-11-05** : le serveur renvoie la révision demandée si elle est dans cette liste, sinon la plus récente supportée. Le transport natif du serveur reste stdio quelle que soit la révision négociée — le pont est la seule pièce qui parle HTTP.

Sur l'exigence de validation de l'en-tête `Origin` posée par la spécification pour un serveur HTTP local, voir la section [Sécurité](#securite-ce-que-tu-exposes-reellement) plus haut.

## Vérification de l'état

```powershell
# Le bridge tourne-t-il ?
Get-NetTCPConnection -LocalPort 8080 -State Listen

# Test direct (local)
Invoke-RestMethod -Uri http://127.0.0.1:8080/mcp -Method POST `
  -Headers @{"Accept"="application/json, text/event-stream"} `
  -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}' `
  -ContentType application/json

# Test via tunnel (remplacer l'URL)
Invoke-RestMethod -Uri https://xxx.trycloudflare.com/mcp ...
```

## Limitations connues

- **Pas de démarrage TopSolid automatique** : si TopSolid n'est pas lancé, les tools `run_recipe` / `execute_script` retournent `Error: TopSolid not connected`. Le bridge, lui, reste vivant.
- **Reconnexion PDM** : si tu fermes TopSolid puis le rouvres sans killer le bridge, les IDs document en cache peuvent être stale. Redémarrer le bridge (`Ctrl+C` + `.\start-bridge.ps1`) résout.
- **Session** : le protocole Streamable HTTP utilise un `Mcp-Session-Id`. claude.ai le gère automatiquement. Pour des tests manuels `curl`, extraire le header `Mcp-Session-Id` de la réponse initialize et le renvoyer sur chaque requête suivante.
- **Pas de HTTPS en local** : `mcp-proxy` écoute en HTTP clair sur `127.0.0.1`. La terminaison TLS est assurée par Cloudflare côté tunnel.
