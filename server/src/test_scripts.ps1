# Script de test corrigé pour Mission 17
$serverPath = ".\bin\Debug\net48\TopSolidMcpServer.exe"

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
