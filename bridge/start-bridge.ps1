# TopSolid MCP Bridge -- start script
#
# Wraps the local stdio TopSolidMcpServer.exe as a Streamable HTTP + SSE server
# so that remote clients (claude.ai web, mobile apps, other hosts) can connect.
#
# Usage:
#     .\start-bridge.ps1                      # bind 127.0.0.1:8080 (local only)
#     .\start-bridge.ps1 -Open                # bind 0.0.0.0:8080 (LAN)
#     .\start-bridge.ps1 -ApiKey "xxxx"       # require X-API-Key header
#     .\start-bridge.ps1 -Port 9000
#
# After starting, expose publicly via a tunnel (see docs/guide/bridge-http.md):
#     cloudflared tunnel --url http://127.0.0.1:8080
#     # -> https://random-name.trycloudflare.com/mcp
#
# claude.ai -> Settings -> Connectors -> Add custom connector
#     URL: https://<your-tunnel>/mcp
# -------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Port = 8080,
    [switch]$Open,
    [string]$ApiKey = "",
    [switch]$DebugProxy
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

# 1. Locate TopSolidMcpServer.exe
if (-not $ExePath) {
    $candidates = @(
        (Join-Path $PSScriptRoot "..\server\src\bin\Release\net48\TopSolidMcpServer.exe"),
        (Join-Path $PSScriptRoot "..\server\src\bin\Debug\net48\TopSolidMcpServer.exe"),
        "C:\TopSolidMCP\TopSolidMcpServer.exe",
        $env:TOPSOLID_MCP_EXE
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { $ExePath = (Resolve-Path $c).Path; break }
    }
}
if (-not $ExePath -or -not (Test-Path $ExePath)) {
    Write-Host "ERROR: TopSolidMcpServer.exe not found." -ForegroundColor Red
    Write-Host "  Pass -ExePath <full-path>, set env TOPSOLID_MCP_EXE, or build server/src first." -ForegroundColor Red
    exit 1
}
Write-Host "[bridge] stdio server: $ExePath" -ForegroundColor Cyan

# 2. Build mcp-proxy args
$bindHost = if ($Open) { "0.0.0.0" } else { "127.0.0.1" }
$proxyArgs = @("mcp-proxy", "--port", "$Port", "--host", $bindHost)
if ($DebugProxy) { $proxyArgs += "--debug" }
if ($ApiKey)     { $proxyArgs += @("--apiKey", $ApiKey) }
$proxyArgs += @("--", $ExePath)

Write-Host "[bridge] HTTP endpoint : http://${bindHost}:${Port}/mcp" -ForegroundColor Green
Write-Host "[bridge] SSE (legacy)  : http://${bindHost}:${Port}/sse" -ForegroundColor Green
if ($ApiKey) {
    Write-Host "[bridge] Auth          : X-API-Key header required" -ForegroundColor Yellow
} else {
    Write-Host "[bridge] Auth          : NONE -- local-only bind; do NOT expose publicly without tunnel + auth." -ForegroundColor Yellow
}
Write-Host ""

# 3. Run
& npx @proxyArgs
