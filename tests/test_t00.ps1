$serverExe = "C:\Users\jup\OneDrive\Cortana\TopSolidMcpServer\src\bin\Debug\net48\TopSolidMcpServer.exe"
$request = @{
    jsonrpc = "2.0"
    id = 99
    method = "tools/call"
    params = @{
        name = "topsolid_execute_script"
        arguments = @{
            code = "var projects = TopSolidHost.Pdm.SearchProjectByName(`"Test MCP`");`nPdmObjectId projectId = PdmObjectId.Empty;`nforeach (var p in projects)`n{`n    if (TopSolidHost.Pdm.GetName(p) == `"Test MCP`")`n    {`n        projectId = p;`n        break;`n    }`n}`nif (projectId.IsEmpty) return `"SETUP FAIL: Projet 'Test MCP' non trouve dans le PDM.`";`n`nvar openProjects = TopSolidHost.Pdm.GetOpenProjects(true, true);`nbool isOpen = false;`nforeach (var op in openProjects)`n{`n    if (TopSolidHost.Pdm.GetName(op) == `"Test MCP`") { isOpen = true; break; }`n}`nif (!isOpen)`n{`n    TopSolidHost.Pdm.OpenProject(projectId);`n}`n`var docs = TopSolidHost.Pdm.SearchDocumentByName(projectId, `"Test 01`");`nPdmObjectId testDocPdm = PdmObjectId.Empty;`nforeach (var d in docs)`n{`n    if (TopSolidHost.Pdm.GetName(d) == `"Test 01`") { testDocPdm = d; break; }`n}`nif (testDocPdm.IsEmpty) return `"SETUP FAIL: Document 'Test 01' non trouve dans le projet 'Test MCP'.`";`n`nvar currentDoc = TopSolidHost.Documents.EditedDocument;`nif (!currentDoc.IsEmpty)`n{`n    var currentPdm = TopSolidHost.Documents.GetPdmObject(currentDoc);`nif (!currentPdm.IsEmpty && TopSolidHost.Pdm.GetName(currentPdm) == `"Test 01`")`n    {`n        return `"SETUP OK: 'Test 01' deja en edition dans 'Test MCP'.`";`n    }`n}`n`nvar docId = TopSolidHost.Documents.GetDocument(testDocPdm);`nif (docId.IsEmpty)`n{`n    var finalMinor = TopSolidHost.Pdm.GetFinalMinorRevision(testDocPdm);`n    if (finalMinor.IsEmpty) return `"SETUP FAIL: Pas de revision for 'Test 01'.`";`n    docId = TopSolidHost.Documents.GetMinorRevisionDocument(finalMinor);`n    if (docId.IsEmpty) return `"SETUP FAIL: Impossible d'obtenir le DocumentId de 'Test 01'.`";`n}`nTopSolidHost.Documents.Open(ref docId);`n`nreturn `"SETUP OK: Projet 'Test MCP' ouvert, document 'Test 01' en edition.`";"
        }
    }
} | ConvertTo-Json -Compress

Write-Host "Sending request to MCP Server..."
$init = '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "TestScript", "version": "1.0"}}}'
$initNotif = '{"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}'

$process = New-Object System.Diagnostics.Process
$process.StartInfo.FileName = $serverExe
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.RedirectStandardInput = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.RedirectStandardError = $true
$process.StartInfo.CreateNoWindow = $true
$process.Start() | Out-Null

$process.StandardInput.WriteLine($init)
$res1 = $process.StandardOutput.ReadLine()
Write-Host "Init Response: $res1"

$process.StandardInput.WriteLine($initNotif)

$process.StandardInput.WriteLine($request)
$res2 = $process.StandardOutput.ReadLine()
Write-Host "T-00 Response: $res2"

$process.Kill()
