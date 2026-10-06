# Endpoint HTTP natif pour claude.ai (et autres clients web)

Depuis la **v1.8.0**, `TopSolidMcpServer.exe` embarque lui-même l'endpoint MCP **Streamable HTTP** — pas de Node.js, pas de process séparé, la validation d'`Origin` et la clé `X-API-Key` gérées nativement. Cela marche pour les clients **web** (claude.ai, ChatGPT, apps mobiles) qui n'ont aucun moyen d'exécuter un .exe sur ta machine : une URL suffit.

::: danger A lire avant de commencer
L'endpoint expose **des outils d'execution de code arbitraire sur votre poste**. `topsolid_execute_script` et `topsolid_modify_script` compilent le C# recu et l'executent en pleine confiance dans le processus du serveur, avec `System.IO` disponible ; les recettes d'ecriture modifient votre PDM. « Lecture seule » signifie ici « hors transaction de modification TopSolid », pas « bac a sable ».

Consequence directe : **un endpoint accessible depuis Internet sans authentification est un acces distant a votre machine.** Mettez [Cloudflare Access](#solution-recommandee-cloudflare-access-gratuit) devant le tunnel avant de donner l'URL a quoi que ce soit. Et si vous n'avez besoin que de consulter, lancez le serveur avec `--read-only` : `topsolid_modify_script` n'est alors pas enregistre du tout.
:::

## Architecture

```
┌────────────────────────┐         HTTPS         ┌────────────────────┐
│ claude.ai (cloud web)  │ ─────────────────────>│ Cloudflare Access  │
└────────────────────────┘                       │ + tunnel nommé     │
                                                 │ (auth obligatoire) │
                                                 └─────────┬──────────┘
                                                           │ HTTPS terminé (l'endpoint natif écoute en HTTP clair)
                                                 ┌─────────v──────────┐
                                                 │ TopSolidMcpServer  │   Streamable HTTP natif /mcp
                                                 │ .exe stdio + HTTP  │   X-API-Key + Origin
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

Le serveur natif parle **Streamable HTTP** (spec MCP 2025-03-26) directement sur `/mcp` — pas de translate, pas de dépendance externe. Le process sert aussi stdio en parallèle (client unique configuré avec `command`), sans conflit.

::: tip v1.8.0+ — serveur HTTP natif et clé API (recommandé)
Depuis la **v1.8.0**, le serveur embarque son propre endpoint HTTP natif (`/mcp` sur le port 8080, redéfinissable par `TOPSOLID_MCP_HTTP_PORT`) **avec authentification `X-API-Key`** : plus besoin du proxy Node pour un usage local ou tunnelisé.

- La clé est **générée, copiée, régénérée et révoquée depuis l'icône tray** (menu *Clé API*). Elle est stockée **chiffrée (DPAPI)** dans `settings.json` — jamais en clair sur le disque.
- Sans clé dans `settings.json` (DPAPI) et sans variable `TOPSOLID_MCP_API_KEY`, l'exe **génère automatiquement une clé au démarrage** et la persiste — l'endpoint n'est jamais « wide open » par accident. Dès qu'une clé existe, elle est **exigée sur chaque requête** (`X-API-Key` header). Le menu tray affiche la clé masquée (4 derniers caractères seulement).
- Les clients distants doivent envoyer l'en-tête `X-API-Key: <votre-cle>` à chaque appel.
:::

## Prérequis

- **TopSolidMcpServer.exe v1.8.0+** (zip de release, ou buildé dans `server/src/bin/Release/net48/`)
- **Cloudflared** (gratuit) pour exposer l'endpoint publiquement — claude.ai est dans le cloud et ne peut pas joindre `localhost` — **avec Cloudflare Access devant** (voir [Sécurité](#securite-ce-que-tu-exposes-reellement))

Installation cloudflared (Windows) :
```powershell
winget install --id Cloudflare.cloudflared
```

## Démarrer le serveur HTTP natif (local uniquement)

**La voie noob (recommandée)** : double-clic `install\Installer_TopSolidMCP.bat` — clé API générée automatiquement (DPAPI), tâche planifiée créée, nettoyage de toute installation bridge v1.7.

**La voie manuelle** :
```powershell
cd C:\TopSolidMCP
.\TopSolidMcpServer.exe --http-standalone
```

Sans l'option `--http-standalone`, si le process est lancé par un client stdio, il tourne quand même : l'endpoint HTTP se branche tout seul (stdin géré par le client).

Sortie attendue :
```
[MCP-INFO] Configured HTTP port: 8080
[MCP-INFO] Starting Native HTTP MCP endpoint on http://+:8080/mcp/
[MCP-INFO] Native HTTP MCP endpoint started on port 8080
[MCP-INFO] http-standalone mode: the process persists after stdin EOF while the HTTP listener is alive
```

::: warning Bind sans droits administrateur
Sans privilege admin ni reservation d'URL (`netsh http add urlacl`), http.sys refuse le bind wildcard `+` : le serveur retombe automatiquement sur `localhost`. Toute requete envoyee avec un Host different (IP Tailscale `100.x.x.x`, nom de machine) recoit alors HTTP 400 « Bad Request - Invalid Hostname ». Pour autoriser l'IP Tailscale/LAN : lancer une fois en admin
`netsh http add urlacl url=http://+:8080/mcp/ user=TOUT_LE_MONDE`
(ou `user=DOMAINE\utilisateur`), ou utiliser le serveur via `http://localhost:8080/mcp` uniquement.
:::

Test rapide :
```powershell
curl -X POST http://127.0.0.1:8080/mcp `
  -H "Accept: application/json, text/event-stream" `
  -H "Content-Type: application/json" `
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"smoke","version":"0.1"}}}'
```

Si tu vois un `event: message` + la réponse JSON-RPC d'`initialize`, l'endpoint natif fonctionne. Avec une clé active, ajoute `-H "X-API-Key: <votre-cle>"` — sans clé valide tu reçois HTTP 401.

## Sécurité — ce que tu exposes réellement

Un endpoint MCP public n'est pas « une API de lecture CAO ». Sans authentification, quiconque connaît l'URL peut :

- **exécuter du code C# arbitraire sur ta machine** via `topsolid_execute_script` / `topsolid_modify_script` : le code est compilé puis exécuté en pleine confiance dans le processus du serveur, avec `System.IO` disponible — donc lecture et écriture de fichiers partout où ton compte Windows le peut ;
- renommer tes pièces, modifier tes paramètres, supprimer des occurrences ;
- exporter ton PDM vers une destination hostile ;
- déclencher des commandes TopSolid arbitraires.

La liste noire de `ScriptExecutor` (`System.Diagnostics.Process`, `System.Net`, `System.Reflection`, `DllImport`, `File.Delete`, `Directory.Delete`, registre, `Environment.Exit`) est un garde-fou contre un modèle qui dérape, **pas** une frontière de sécurité — et `TOPSOLID_MCP_ALLOW_UNSAFE=1` la supprime.

Deux réflexes avant d'exposer l'endpoint :

1. **Authentifier le tunnel** (section suivante) — ou, pour les clients que tu contrôles, activer la clé `X-API-Key` (générée automatiquement ou depuis le tray ; requête reçue sans la bonne clé = HTTP 401) ;
2. lancer le serveur avec **`--read-only`** (ou `TOPSOLID_MCP_READ_ONLY=1`) si tu n'as besoin que de consulter : `topsolid_modify_script` n'est alors pas enregistré du tout et les recettes tournent en lecture seule.

### Le problème avec claude.ai + auth

claude.ai accepte deux modes d'auth côté UI :
- **Aucun** (anonyme)
- **OAuth 2.1** (Client ID + Client Secret) — nécessite un serveur OAuth complet, lourd

**Ce qui n'est PAS supporté** : Bearer token statique, `X-API-Key`, HTTP Basic. Le serveur expose pourtant l'auth par clé (`X-API-Key` requise dès qu'une clé existe) mais claude.ai ne peut pas l'envoyer ([issue claude-ai-mcp#112](https://github.com/anthropics/claude-ai-mcp/issues/112)).

D'où la solution ci-dessous pour claude.ai : exposer l'URL derrière Cloudflare Access, et laisser la clé `X-API-Key` protéger les clients que tu contrôles (Claude Desktop, clients LAN, tunnel authentifié configurable).

## Solution recommandée : Cloudflare Access (gratuit)

**C'est la seule configuration recommandée pour exposer l'endpoint publiquement.** CF Access garde l'URL derrière une politique par email : seul ton email peut obtenir un token, ajouté automatiquement via cookies côté navigateur ou JWT côté API.

Setup (résumé, voir les docs Cloudflare pour le détail) :

1. Créer un compte **Cloudflare Zero Trust** (free tier : 50 users)
2. Créer un tunnel **nommé** attaché à ton domaine : `cloudflared tunnel create topsolid-mcp` + route DNS
3. Créer une **Application Access** sur `topsolid-mcp.tondomaine.com` → Policy : `include: emails = toi@example.com`
4. Dans claude.ai, donner l'URL `https://topsolid-mcp.tondomaine.com/mcp` (noter le `/mcp`). CF intercepte, force l'auth, le backend Anthropic s'authentifie via une **service token** CF (à configurer en "bypass for CF tokens")

C'est la voie officielle pour ce use case et ça reste 100 % gratuit.

### Validation de l'en-tête `Origin`

La spécification MCP exige qu'un serveur HTTP local **valide l'en-tête `Origin` de chaque requête entrante** et rejette celles qui viennent d'une origine inattendue, précisément pour bloquer les attaques DNS-rebinding qui transforment une page web visitée par l'utilisateur en client de ton serveur local. Elle recommande également de ne binder que sur `127.0.0.1`.

**C'est implémenté côté serveur depuis la v1.8.0** : sans `Origin` (CLI, clients MCP, pairs Tailscale) ou depuis `localhost`/`127.0.0.1`/`[::1]` la requête passe ; toute autre origine est rejetée en HTTP 403 (JSON-RPC `-32002`). Une origine supplémentaire (client web LAN) peut être ajoutée via la variable d'environnement `TOPSOLID_MCP_ALLOWED_ORIGINS` (séparée par des virgules). Le CORS renvoyé est réfléchi : seul l'`Origin` autorisé est écho, jamais `*`.

### Clé d'API de l'endpoint natif

La clé vit dans `settings.json` chiffré DPAPI, à côté de l'exe. Trois façons de la définir :

1. **Automatique** — au premier démarrage sans clé, l'exe en génère une et la persiste (affichée dans la console) ;
2. **Depuis le tray** — menu *Clé API* : afficher / copier / régénérer / révoquer ;
3. **Par environnement `TOPSOLID_MCP_API_KEY`** — pour les serveurs headless (session 0) où DPAPI/tray n'est pas dispo ; la variable est relue à chaque requête, jamais sur la ligne de commande — les arguments de processus sont lisibles par n'importe quel utilisateur local et finissent dans les journaux d'audit.

```powershell
$env:TOPSOLID_MCP_API_KEY = "<cle>"
.\TopSolidMcpServer.exe --http-standalone
```

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
Cette URL est **publique et sans aucune authentification** : elle contourne la clé `X-API-Key` que Cloudflare Access n'envoie pas pour toi. Elle donne à quiconque la découvre l'exécution de code arbitraire sur ton poste (voir la section Sécurité ci-dessus). L'obscurité du nom aléatoire n'est pas une protection : les domaines `trycloudflare.com` sont scannés.

Si tu l'utilises malgré tout, pour un POC strictement local et court :
- ne partage JAMAIS l'URL ;
- lance le serveur en `--read-only` ;
- coupe le tunnel dès que tu ne l'utilises plus (`Ctrl+C`) ;
- ne binde pas le serveur sur `0.0.0.0` (via `netsh http add urlacl` wildcard) : un listener LAN reste une surface inutile ici.
:::

## Brancher claude.ai

1. Ouvrir **claude.ai** → **Settings** → **Connecteurs** → **Ajouter un connecteur personnalisé**
2. **URL du serveur MCP distant** : `https://topsolid-mcp.tondomaine.com/mcp` (l'URL protégée par CF Access)
3. **Ajouter**

claude.ai envoie un `initialize` puis `tools/list` — tu dois voir apparaître les **13 outils TopSolid** dans l'UI du connecteur (12 si le serveur tourne en `--read-only`).

## Spec MCP utilisée

L'endpoint natif implémente **Streamable HTTP** (MCP spec `2025-03-26`) :
- `POST /mcp` : requête/réponse JSON-RPC, réponse en `text/event-stream` (SSE) ou `application/json`
- `GET /mcp` : canal SSE passif pour les notifications serveur → client
- `DELETE /mcp` : termine la session (header `Mcp-Session-Id`)
- Header `Mcp-Session-Id` défini sur `InitializeResult`, écho obligatoire sur requêtes suivantes

Côté serveur, la négociation de révision à l'`initialize` accepte **2025-06-18**, **2025-03-26** et **2024-11-05** : le serveur renvoie la révision demandée si elle est dans cette liste, sinon la plus récente supportée. Le même process expose stdio en parallèle — les deux transports parlent le même jeu d'outils.

Sur l'exigence de validation de l'en-tête `Origin` posée par la spécification pour un serveur HTTP local, voir la section [Validation de l'en-tête `Origin`](#validation-de-l-en-tete-origin) ci-dessus.

## Vérification de l'état

```powershell
# Le serveur tourne-t-il ?
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

- **Pas de démarrage TopSolid automatique** : si TopSolid n'est pas lancé, les tools `run_recipe` / `execute_script` retournent `Error: TopSolid not connected`. Le serveur, lui, reste vivant.
- **Reconnexion TopSolid** : sans `--no-tray`, le tray géré par l'exe gère la reconnexion automatique (backoff 15/30/60 s). En cas de souci, redémarrer le serveur (menu tray **Redémarrer**, ou tuer le process et relancer) résout.
- **Session** : le protocole Streamable HTTP utilise un `Mcp-Session-Id`. claude.ai le gère automatiquement. Pour des tests manuels `curl`, extraire le header `Mcp-Session-Id` de la réponse initialize et le renvoyer sur chaque requête suivante.
- **Pas de HTTPS en local** : l'endpoint natif écoute en HTTP clair sur `127.0.0.1`/`localhost`. La terminaison TLS est assurée par Cloudflare côté tunnel.
- **Sans Tailscale/LAN sans admin** : voir l'encadré [Bind sans droits administrateur](#demarrer-le-serveur-http-natif-local-uniquement) — sans URL ACL, seul le Host `localhost` est accepté.
