# Script de test corrigé pour Mission 17
# Server executable resolution:
#   1. $env:TOPSOLID_MCP_EXE
#   2. the Debug build next to this script (server/src/bin/Debug/net48/)
# No personal path is hard-coded here.
$serverPath = $env:TOPSOLID_MCP_EXE
if (-not $serverPath) {
    $serverPath = Join-Path $PSScriptRoot 'bin\Debug\net48\TopSolidMcpServer.exe'
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
    
    # On utilise "Wait-Process" ou une gestion de flux si nécessaire, mais ici le serveur attend stdin et répond sur stdout
    # Pour un test unitaire simple, on pipe l'entrée
    $request | & $serverPath | Out-String
}

Write-Host "--- Test 1 : Lecture Simple ---"
$code1 = 'var doc = TopSolidHost.Documents.EditedDocument; return TopSolidHost.Documents.GetName(doc);'
Send-McpRequest "tools/call" @{ name = "topsolid_execute_script"; arguments = @{ code = $code1 } }

Write-Host "`n--- Test 2 : Liste des Éléments ---"
$code2 = 'var doc = TopSolidHost.Documents.EditedDocument; var elements = TopSolidHost.Elements.GetElements(doc); var names = elements.Select(e => TopSolidHost.Elements.GetName(e)); return string.Join("\n", names);'
Send-McpRequest "tools/call" @{ name = "topsolid_execute_script"; arguments = @{ code = $code2 } }

Write-Host "`n--- Test 3 : Liste des Esquisses ---"
$code3 = 'var doc = TopSolidHost.Documents.EditedDocument; var elements = TopSolidHost.Elements.GetElements(doc); var sketches = elements.Where(e => TopSolidHost.Elements.GetTypeFullName(e).Contains("Sketch")); return string.Join("\n", sketches.Select(e => TopSolidHost.Elements.GetName(e)));'
Send-McpRequest "tools/call" @{ name = "topsolid_execute_script"; arguments = @{ code = $code3 } }
