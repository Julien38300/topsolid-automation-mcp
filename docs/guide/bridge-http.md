# Bridge HTTP/SSE pour claude.ai (et autres clients web)

Le `TopSolidMcpServer.exe` est un serveur MCP **stdio local** — il communique par stdin/stdout et vit dans le même processus que son client (Claude Desktop, Claude Code CLI, etc.). Cela ne marche pas pour les clients **web** (claude.ai, ChatGPT, apps mobiles) qui n'ont aucun moyen d'exécuter un .exe sur ta machine.

Ce guide installe un **pont HTTP/SSE** qui enveloppe le serveur stdio dans un endpoint HTTP. Les clients distants s'y connectent par URL.

## Architecture

```
┌────────────────────────┐         HTTPS         ┌────────────────────┐
│ claude.ai (cloud web)  │ ─────────────────────>│ Cloudflare Tunnel  │
└────────────────────────┘                       │ (public URL)       │
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
- **Cloudflared** (gratuit) ou **ngrok** pour exposer le bridge publiquement — claude.ai est dans le cloud et ne peut pas joindre `localhost`

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
[bridge] stdio server : C:\Users\...\TopSolidMcpServer.exe
[bridge] HTTP endpoint : http://127.0.0.1:8080/mcp
[bridge] SSE (legacy)  : http://127.0.0.1:8080/sse
[bridge] Auth          : NONE -- local-only bind; do NOT expose publicly without tunnel + auth.
```

Test rapide :
```powershell
curl -X POST http://127.0.0.1:8080/mcp `
  -H "Accept: application/json, text/event-stream" `
  -H "Content-Type: application/json" `
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"smoke","version":"0.1"}}}'
```

Si tu vois un `event: message` + la réponse JSON-RPC d'`initialize`, le bridge fonctionne.

## Exposer publiquement avec cloudflared

Dans un **second terminal** (laisser le bridge tourner) :

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

**URL à donner à claude.ai** : `https://random-words.trycloudflare.com/mcp` (noter le `/mcp`).

## Brancher claude.ai

1. Ouvrir **claude.ai** → **Settings** → **Connecteurs** → **Ajouter un connecteur personnalisé**
2. **URL du serveur MCP distant** : `https://<your-tunnel>.trycloudflare.com/mcp`
3. Laisser les champs OAuth vides (le connecteur passera en mode anonyme)
4. **Ajouter**

claude.ai envoie un `initialize` puis `tools/list` — tu dois voir apparaître les **12 outils TopSolid** dans l'UI du connecteur.

## ⚠️ Sécurité — à lire avant d'exposer publiquement

**Les opérations d'écriture TopSolid sont destructives.** Un MCP server exposé sans auth = quiconque connaît l'URL peut :
- renommer tes pièces, modifier tes paramètres, supprimer des occurrences
- exporter ton PDM vers une destination hostile
- lancer `invoke_command` avec des commandes TopSolid arbitraires

### Le problème avec claude.ai + auth

claude.ai accepte deux modes d'auth côté UI :
- **Aucun** (anonyme) — ce qu'on vient d'utiliser
- **OAuth 2.1** (Client ID + Client Secret) — nécessite un serveur OAuth complet côté bridge, lourd

**Ce qui n'est PAS supporté** : Bearer token statique, `X-API-Key`, HTTP Basic. L'option `--apiKey` de `mcp-proxy` existe mais claude.ai ne peut pas l'envoyer ([issue claude-ai-mcp#112](https://github.com/anthropics/claude-ai-mcp/issues/112)).

### Solution pragmatique : Cloudflare Access (gratuit, 10 min)

L'idée : garder le bridge authless, mais mettre **Cloudflare Access** devant le tunnel. CF Access gate l'URL par email — seul ton email peut requester un token, et il est ajouté automatiquement via cookies côté browser ou JWT côté API.

Setup (résumé, voir docs CF pour le détail) :
1. Créer un compte **Cloudflare Zero Trust** (free tier : 50 users)
2. Remplacer le `tunnel --url` éphémère par un tunnel **nommé** attaché à ton domaine : `cloudflared tunnel create topsolid-mcp` + route DNS
3. Créer une **Application Access** sur `topsolid-mcp.tondomaine.com` → Policy : `include: emails = toi@example.com`
4. Dans claude.ai, donner l'URL `https://topsolid-mcp.tondomaine.com/mcp`. CF intercepte, force l'auth, Anthropic backend s'auth via une **service token** CF (à configurer en "bypass for CF tokens")

C'est la voie officielle pour ce use case et ça reste 100% gratuit.

### Solution provisoire (dev uniquement)

Pour un POC rapide **sans** Cloudflare Access :
- `cloudflared tunnel --url http://127.0.0.1:8080` — URL `trycloudflare.com` aléatoire, non indexée
- Ne partage JAMAIS l'URL
- Stop le tunnel quand tu ne l'utilises pas (`Ctrl+C`)
- N'active JAMAIS `.\start-bridge.ps1 -Open` (bind `0.0.0.0`) sans tunnel + auth

Le risque résiduel : si un attaquant scanne trycloudflare.com et essaye des noms aléatoires, il pourrait tomber sur le tien. La probabilité est faible, mais non-nulle.

## Spec MCP utilisée

Le bridge implémente **Streamable HTTP** (MCP spec `2025-03-26`) :
- `POST /mcp` : requête/réponse JSON-RPC, réponse en `text/event-stream` (SSE) ou `application/json`
- `GET /mcp` : canal SSE passif pour les notifications serveur → client
- `DELETE /mcp` : termine la session (header `Mcp-Session-Id`)
- Header `Mcp-Session-Id` défini sur `InitializeResult`, écho obligatoire sur requêtes suivantes

Le bridge expose également l'ancien transport **HTTP+SSE** (`2024-11-05`) sur `/sse` + `/messages` pour compat clients legacy (hors claude.ai, qui est à jour).

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
