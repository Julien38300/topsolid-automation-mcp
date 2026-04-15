$serverPath = "..\src\bin\Debug\net48\TopSolidMcpServer.exe"

function Send-McpRequest($method, $params) {
    # Handshake MCP
    $initRequest = @{ jsonrpc = "2.0"; id = 1; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "TestClient"; version = "1.0" } } } | ConvertTo-Json -Compress
    $initNotif = @{ jsonrpc = "2.0"; method = "notifications/initialized"; params = @{} } | ConvertTo-Json -Compress
    $request = @{ jsonrpc = "2.0"; id = 2; method = $method; params = $params } | ConvertTo-Json -Compress

    # Pipe multiple requests to stdin
    $input = "$initRequest`n$initNotif`n$request"
    $response = $input | & $serverPath 2>$null
    return $response
}

function Run-Test($id, $name, $tool, $args) {
    Write-Host "`n--- [$id] $name ---" -ForegroundColor Cyan
    $response = Send-McpRequest "tools/call" @{ name = $tool; arguments = $args } | Out-String
    
    # On cherche le champ "text" dans le JSON de réponse via regex (plus simple en PS que full parse)
    if ($response -match '"text":"(.*?)"') {
        $text = $matches[1].Replace('\n', " ").Replace('\"', '"')
        if ($text.Contains("Erreur de compilation")) {
            Write-Host "FAILED: Compilation Error" -ForegroundColor Red
            Write-Host $text -ForegroundColor Gray
        } else {
            Write-Host "SUCCESS: $text" -ForegroundColor Green
        }
    } else {
        Write-Host "RAW RESPONSE: $response" -ForegroundColor Gray
    }
}

# Execution des tests un par un
Run-Test "T-92" "detect drafting document" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; bool isDrafting = TopSolidDraftingHost.Draftings.IsDrafting(docId); return `"IsDrafting=`" + isDrafting;" }
Run-Test "T-93" "detect bom document" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; bool isBom = TopSolidDraftingHost.Boms.IsBom(docId); return `"IsBom=`" + isBom;" }
Run-Test "T-94" "detect unfolding document" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; bool isUnfolding = TopSolidDesignHost.Unfoldings.IsUnfolding(docId); return `"IsUnfolding=`" + isUnfolding;" }
Run-Test "T-95" "list drafting views" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; if (!TopSolidDraftingHost.Draftings.IsDrafting(docId)) return `"Not a drafting.`"; var views = TopSolidDraftingHost.Draftings.GetDraftingViews(docId); return `"Vues:`" + views.Count;" }
Run-Test "T-96" "read bom columns" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; if (!TopSolidDraftingHost.Boms.IsBom(docId)) return `"Not a BOM.`"; int colCount = TopSolidDraftingHost.Boms.GetColumnCount(docId); return `"colonnes:`" + colCount;" }
Run-Test "T-97" "read bom rows" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; if (!TopSolidDraftingHost.Boms.IsBom(docId)) return `"Not a BOM.`"; int rootRow = TopSolidDraftingHost.Boms.GetRootRow(docId); var children = TopSolidDraftingHost.Boms.GetRowChildrenRows(docId, rootRow); return `"Children:`" + children.Count + `" |`";" }
Run-Test "T-98" "read drafting tables" "topsolid_execute_script" @{ code = "DocumentId docId = TopSolidHost.Documents.EditedDocument; if (docId.IsEmpty) return `"Aucun document ouvert.`"; if (!TopSolidDraftingHost.Draftings.IsDrafting(docId)) return `"Not a drafting.`"; var tables = TopSolidDraftingHost.Tables.GetDraftTables(docId); return `"Tableaux:`" + tables.Count;" }
Run-Test "T-99" "api_help mise en plan" "topsolid_api_help" @{ query = "mise en plan" }
