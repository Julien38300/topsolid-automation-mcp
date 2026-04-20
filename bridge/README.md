# TopSolid MCP HTTP/SSE Bridge

Exposes the local stdio `TopSolidMcpServer.exe` as a **Streamable HTTP + SSE** MCP server so that remote clients (claude.ai web, mobile apps, server-side agents) can connect.

Implemented as a thin wrapper around [`mcp-proxy`](https://github.com/punkpeye/mcp-proxy) — no custom protocol code.

## Install

```powershell
cd bridge
npm install
```

## Run

**Local only (127.0.0.1):**
```powershell
.\start-bridge.ps1
# -> http://127.0.0.1:8080/mcp    (Streamable HTTP, recommended)
# -> http://127.0.0.1:8080/sse    (legacy HTTP+SSE, back-compat)
```

**With API key auth (requires `X-API-Key` header):**
```powershell
.\start-bridge.ps1 -ApiKey "your-secret"
```

**Custom port / LAN binding:**
```powershell
.\start-bridge.ps1 -Port 9000 -Open
```

## Expose publicly (required for claude.ai web)

claude.ai is cloud — it cannot reach `localhost` on your PC. Use a tunnel:

```powershell
# Cloudflare (free, recommended)
cloudflared tunnel --url http://127.0.0.1:8080
# -> https://<random>.trycloudflare.com

# Or ngrok
ngrok http 8080
```

Then in claude.ai: **Settings → Connecteurs → Ajouter un connecteur personnalisé** → paste `https://<tunnel>/mcp`.

## Security

Read `/guide/bridge-http` in the docs for the full threat model. Short version:

- **Never expose the bridge publicly without auth.** TopSolid write operations are destructive.
- `mcp-proxy`'s `--apiKey` only works for clients that can send `X-API-Key`. claude.ai does **not** support that header today.
- For claude.ai: front the tunnel with **Cloudflare Access** (Zero Trust, free tier) — gate by email. The bridge stays authless; CF Access rejects unauthenticated traffic.
- Always `--host 127.0.0.1` + tunnel. Never `--host 0.0.0.0` to the internet.

See `docs/guide/bridge-http.md` for the full setup.
