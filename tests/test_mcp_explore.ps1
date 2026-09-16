# Smoke test: topsolid_explore_paths over stdio.
#
# Server executable resolution:
#   1. $env:TOPSOLID_MCP_EXE
#   2. the repo-local Debug build (server/src/bin/Debug/net48/)
# No personal path is hard-coded here.

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$serverExe = $env:TOPSOLID_MCP_EXE
if (-not $serverExe) {
    $serverExe = Join-Path $scriptDir '..\server\src\bin\Debug\net48\TopSolidMcpServer.exe'
}
if (-not (Test-Path $serverExe)) {
    Write-Host "ERROR: TopSolidMcpServer.exe not found at: $serverExe" -ForegroundColor Red
    Write-Host "  Build server/src first, or set `$env:TOPSOLID_MCP_EXE to the executable path." -ForegroundColor Red
    exit 1
}

$request = '{"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "topsolid_explore_paths", "arguments": {"sourceType": "IElements", "targetType": "System.Collections.Generic.List", "maxDepth": 1}}}'
$request | & $serverExe
