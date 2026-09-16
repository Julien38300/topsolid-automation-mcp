# TopSolid MCP Bridge -- start script
#
# Wraps the local stdio TopSolidMcpServer.exe as a Streamable HTTP + SSE server
# so that remote clients (claude.ai web, mobile apps, other hosts) can connect.
#
# WHAT THIS ENDPOINT EXPOSES: every tool of the MCP server, including
# topsolid_execute_script and topsolid_modify_script, which compile and run
# arbitrary C# inside the TopSolid process. Whoever can reach this URL can run
# code on this workstation. Read bridge/README.md before exposing it anywhere.
#
# Usage:
#     .\start-bridge.ps1                       # bind 127.0.0.1:8080 (local only)
#     .\start-bridge.ps1 -RequireApiKey        # local, but refuse to start without a key
#     .\start-bridge.ps1 -Open                 # bind 0.0.0.0:8080 (LAN) -- key REQUIRED
#     .\start-bridge.ps1 -Port 9000
#
# API key: set it in the environment, not on the command line.
#     $env:TOPSOLID_MCP_API_KEY = "<key>"      # current PowerShell session only
# The key is handed to mcp-proxy through the child process environment
# (MCP_PROXY_API_KEY, which yargs maps to --apiKey). It is never put on a command
# line: process arguments are readable by any local user (tasklist /v,
# Get-CimInstance Win32_Process) and are recorded by process-audit logs
# (Windows event 4688, Sysmon event 1).
#
# Remote access: put Cloudflare Access in front of the tunnel -- a bare tunnel
# publishes remote code execution to the internet. See bridge/README.md.
#     cloudflared tunnel --url http://127.0.0.1:8080
# -------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Port = 8080,
    [switch]$Open,
    [switch]$RequireApiKey,
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

# 2. Resolve the API key -- environment first, -ApiKey only as a fallback.
$resolvedKey = ""
if ($ApiKey) {
    $resolvedKey = $ApiKey
    Write-Host "[bridge] WARNING: -ApiKey puts the key on this script's own command line," -ForegroundColor Yellow
    Write-Host "[bridge]          where any local user and the process-audit log can read it." -ForegroundColor Yellow
    Write-Host "[bridge]          Prefer: `$env:TOPSOLID_MCP_API_KEY = '<key>'" -ForegroundColor Yellow
} elseif ($env:TOPSOLID_MCP_API_KEY) {
    $resolvedKey = $env:TOPSOLID_MCP_API_KEY
}

if ($Open -and -not $resolvedKey) {
    Write-Host "ERROR: -Open binds 0.0.0.0 (every interface) and requires an API key." -ForegroundColor Red
    Write-Host "  This endpoint exposes topsolid_execute_script / topsolid_modify_script:" -ForegroundColor Red
    Write-Host "  an unauthenticated listener on the LAN is remote code execution on this PC." -ForegroundColor Red
    Write-Host "  Set the key first:  `$env:TOPSOLID_MCP_API_KEY = '<key>'" -ForegroundColor Red
    Write-Host "  Safer alternative: keep the default 127.0.0.1 bind and tunnel it behind" -ForegroundColor Red
    Write-Host "  Cloudflare Access (see bridge/README.md)." -ForegroundColor Red
    exit 1
}

if ($RequireApiKey -and -not $resolvedKey) {
    Write-Host "ERROR: -RequireApiKey was passed but no key is available." -ForegroundColor Red
    Write-Host "  Set it first:  `$env:TOPSOLID_MCP_API_KEY = '<key>'" -ForegroundColor Red
    exit 1
}

# 3. Build mcp-proxy args.
#    --host is always explicit: mcp-proxy defaults to "::", i.e. every interface.
#    The key is NOT an argument -- it travels through the child environment below.
$bindHost = if ($Open) { "0.0.0.0" } else { "127.0.0.1" }
$proxyArgs = @("mcp-proxy", "--port", "$Port", "--host", $bindHost)
if ($DebugProxy) { $proxyArgs += "--debug" }
$proxyArgs += @("--", $ExePath)

# mcp-proxy parses its options with yargs .env("MCP_PROXY"), so MCP_PROXY_API_KEY
# is read as --apiKey. Verified against mcp-proxy 6.4.6 (the pinned version).
#
# That is the WHOLE authentication story, and it is version-dependent. If
# bridge/node_modules is missing, `npx` silently downloads the latest mcp-proxy
# instead of the 6.4.6 pinned in package-lock.json -- a release that dropped
# .env("MCP_PROXY") would start an authless listener while this script still
# prints "Auth: X-API-Key header required". Fail closed when a key is in play.
$localProxy = Join-Path $PSScriptRoot "node_modules\mcp-proxy\package.json"
if (-not (Test-Path $localProxy)) {
    if ($resolvedKey) {
        Write-Host "ERROR: bridge/node_modules/mcp-proxy is missing." -ForegroundColor Red
        Write-Host "  npx would fetch an unpinned mcp-proxy, and API-key auth is only" -ForegroundColor Red
        Write-Host "  verified for the 6.4.6 pinned in package-lock.json. Refusing to" -ForegroundColor Red
        Write-Host "  claim authentication we cannot guarantee." -ForegroundColor Red
        Write-Host "  Run:  cd bridge; npm install" -ForegroundColor Red
        exit 1
    }
    Write-Host "[bridge] WARNING: bridge/node_modules/mcp-proxy is missing; npx will fetch" -ForegroundColor Yellow
    Write-Host "[bridge]          an unpinned version. Run 'npm install' in bridge/." -ForegroundColor Yellow
}

if ($resolvedKey) {
    $env:MCP_PROXY_API_KEY = $resolvedKey
} elseif (Test-Path Env:MCP_PROXY_API_KEY) {
    Remove-Item Env:MCP_PROXY_API_KEY
}

Write-Host "[bridge] HTTP endpoint : http://${bindHost}:${Port}/mcp" -ForegroundColor Green
Write-Host "[bridge] SSE (legacy)  : http://${bindHost}:${Port}/sse" -ForegroundColor Green
if ($resolvedKey) {
    Write-Host "[bridge] Auth          : X-API-Key header required" -ForegroundColor Yellow
} else {
    Write-Host "[bridge] Auth          : NONE -- 127.0.0.1 bind only." -ForegroundColor Yellow
    Write-Host "[bridge]                  A local bind is not a security boundary: any page your" -ForegroundColor Yellow
    Write-Host "[bridge]                  browser loads can POST to 127.0.0.1 unless the Origin" -ForegroundColor Yellow
    Write-Host "[bridge]                  header is validated. See bridge/README.md." -ForegroundColor Yellow
}
Write-Host ""

# 4. Run
& npx @proxyArgs
