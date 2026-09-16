# Manual check of topsolid_explore_paths over stdio.
#
# Server executable resolution:
#   1. $env:TOPSOLID_MCP_EXE
#   2. the Debug build next to this script (server/src/bin/Debug/net48/)
# No personal path is hard-coded here.

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$serverPath = $env:TOPSOLID_MCP_EXE
if (-not $serverPath) {
    $serverPath = Join-Path $scriptDir 'bin\Debug\net48\TopSolidMcpServer.exe'
}
if (-not (Test-Path $serverPath)) {
    Write-Host "ERROR: TopSolidMcpServer.exe not found at: $serverPath" -ForegroundColor Red
    Write-Host "  Build server/src first, or set `$env:TOPSOLID_MCP_EXE to the executable path." -ForegroundColor Red
    exit 1
}

function Send-McpRequest($method, $params) {
    $request = @{
        jsonrpc = "2.0"
        id = 1
        method = $method
        params = $params
    } | ConvertTo-Json -Compress

    $request | & $serverPath | Out-String
}

Write-Host "--- Test Explore Paths (IPdm -> String) ---"
Send-McpRequest "tools/call" @{
    name = "topsolid_explore_paths";
    arguments = @{
        sourceType = "IPdm";
        targetType = "String";
        maxDepth = 3
    }
}

Write-Host "`n--- Test No Path (String -> IPdm) ---"
Send-McpRequest "tools/call" @{
    name = "topsolid_explore_paths";
    arguments = @{
        sourceType = "String";
        targetType = "IPdm";
        maxDepth = 2
    }
}
