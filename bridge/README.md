# TopSolid MCP HTTP/SSE Bridge

Exposes the local stdio `TopSolidMcpServer.exe` as a **Streamable HTTP + SSE** MCP server so that remote clients (claude.ai web, mobile apps, server-side agents) can connect.

Implemented as a thin wrapper around [`mcp-proxy`](https://github.com/punkpeye/mcp-proxy) — no custom protocol code.

## What you are actually exposing

The bridge publishes **every tool of the MCP server**, including `topsolid_execute_script` and `topsolid_modify_script`. Those two compile user-supplied C# with `CSharpCodeProvider` and run it **in-process, fully trusted**, inside TopSolid: `System.IO` is available, `ScriptExecutor.BlockedSymbols` is a guardrail against a model wandering off, not a sandbox.

So: *anything that can reach this endpoint can run arbitrary code on this workstation, and write to the PDM vault.* Treat the URL as a remote shell, not as an API. Everything below follows from that.

Two mitigations are not optional if the endpoint leaves `127.0.0.1`:

- an identity check in front (Cloudflare Access), and
- the `--read-only` flag / `TOPSOLID_MCP_READ_ONLY=1` on the server itself when you only need reads — `topsolid_modify_script` is then not registered at all.

## Install

```powershell
cd bridge
npm install
```

## Run

**Local only (127.0.0.1) — the default:**
```powershell
.\start-bridge.ps1
# -> http://127.0.0.1:8080/mcp    (Streamable HTTP, recommended)
# -> http://127.0.0.1:8080/sse    (legacy HTTP+SSE, back-compat)
```

**With API key auth (clients must send `X-API-Key`):**
```powershell
$env:TOPSOLID_MCP_API_KEY = "<key>"   # current session only
.\start-bridge.ps1 -RequireApiKey     # or: npm run start:auth
```

The key is read from the `TOPSOLID_MCP_API_KEY` environment variable and forwarded to `mcp-proxy` through the child process environment (`MCP_PROXY_API_KEY`, which yargs maps to `--apiKey`). **Never pass a key as a command-line argument**: process arguments are readable by any local user (`tasklist /v`, `Get-CimInstance Win32_Process`) and are copied verbatim into process-audit logs (Windows event 4688, Sysmon event 1). The `-ApiKey` parameter still exists for compatibility and warns when used.

**LAN binding** (`-Open`, i.e. `--host 0.0.0.0`) now refuses to start without a key — but `docs/guide/bridge-http.md` advises against using it at all. Keep the `127.0.0.1` bind and tunnel it behind Cloudflare Access instead.

## Remote access

### Recommended: tunnel behind Cloudflare Access

claude.ai is cloud — it cannot reach `localhost` on your PC, so the endpoint has to be published somehow. Publish it **behind an identity gate**:

1. Keep the bridge on `127.0.0.1` (the default).
2. Create a named Cloudflare tunnel pointing at `http://127.0.0.1:8080`.
3. In Cloudflare Zero Trust, put an **Access** application on the tunnel hostname and gate it by email / identity provider. The free tier covers a handful of users.

Cloudflare Access rejects unauthenticated traffic before it reaches your machine, which is what you want given the previous section. The bridge itself stays authless in this setup, because claude.ai cannot send a custom `X-API-Key` header today (see `docs/guide/bridge-http.md`) — Access is the authentication, not a second layer on top of one.

Then in claude.ai: **Settings → Connectors → Add custom connector** → paste `https://<your-hostname>/mcp`.

### Not recommended: a bare tunnel

```powershell
cloudflared tunnel --url http://127.0.0.1:8080   # quick tunnel, no Access
ngrok http 8080
```

A quick tunnel gives you a public URL with **no authentication at all**. The hostname is random, but it is not a secret: it travels through DNS and TLS certificate transparency logs, and it is trivially scanned. For the duration of the tunnel, anyone who guesses or observes the URL has remote code execution on the machine running TopSolid. Use this only for a short local test, on a machine with nothing valuable open, and stop it when you are done.

## Local binding is not a security boundary either

The MCP specification requires a local HTTP server to **validate the `Origin` header** on incoming requests, precisely because binding `127.0.0.1` does not keep browsers out. A malicious web page the user happens to open can make their browser POST to `http://127.0.0.1:8080/mcp` (DNS rebinding / CSRF against localhost); the request comes from the loopback interface and looks local.

This bridge is a thin wrapper and **adds no `Origin` validation of its own**, and `mcp-proxy` does not implement it either (see `docs/guide/bridge-http.md`). Nothing in this stack rejects a request coming from an unexpected origin. Concretely:

- run the bridge only while you need it, not as a background service;
- set an API key even for the local bind if the machine also browses the web — an `X-API-Key` header is one thing a cross-origin page cannot add without a successful CORS preflight;
- use `--read-only` / `TOPSOLID_MCP_READ_ONLY=1` whenever writes are not needed.

## Security checklist

- [ ] Bind `127.0.0.1` (the default). Avoid `-Open`; it refuses to start without a key anyway.
- [ ] Key in `TOPSOLID_MCP_API_KEY`, never on a command line, never committed.
- [ ] Public exposure goes through Cloudflare Access, not a bare tunnel.
- [ ] `--read-only` unless you specifically need the write tools.
- [ ] Never auto-approve `topsolid_execute_script` / `topsolid_modify_script` in your MCP client.

See `docs/guide/bridge-http.md` for the full setup.
