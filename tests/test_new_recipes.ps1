$serverPath = Join-Path $PSScriptRoot "..\src\bin\Debug\net48\TopSolidMcpServer.exe"

Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "  TESTS LIVE NOUVELLES RECETTES - Session 2026-04-10b" -ForegroundColor Yellow
Write-Host "  Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow

$initReq = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"NewRecipeTest","version":"1.0"}}}'
$initNotif = '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

$tests = @(
    # MASSE/VOLUME fixes (5)
    @("MV-01", "read_mass_volume",         "", "Masse:"),
    @("MV-02", "read_material_density",     "", ""),
    @("MV-03", "assembly_mass_report",  "", "RAPPORT MASSE"),
    @("MV-04", "read_material",             "", ""),
    @("MV-05", "check_missing_materials","","VERIFICATION MATERIAUX"),

    # DIMENSIONS nouvelles (3)
    @("DM-01", "read_part_dimensions",     "", "DIMENSIONS"),
    @("DM-02", "read_inertia_moments",      "", "MOMENTS"),
    @("DM-03", "read_bounding_box",     "", "Boite englobante:"),

    # M-58 MISE EN PLAN (3) - skip si pas un plan ouvert
    @("MP-01", "detect_drafting",     "", ""),
    @("MP-02", "detect_bom",     "", ""),
    @("MP-03", "detect_unfolding",      "", ""),

    # BATCH bibliotheque (7)
    @("BT-01", "summarize_project",            "", "Projet:"),
    @("BT-02", "count_documents_by_type","", "Total:"),
    @("BT-03", "list_documents_without_reference","","Documents sans reference:"),
    @("BT-04", "list_documents_without_designation","","Documents sans designation:"),
    @("BT-05", "list_project_documents",   "", "Projet:"),
    @("BT-06", "search_parts_by_material","","Pieces trouvees:"),
    @("BT-07", "read_where_used",           "", "Cas d'emploi:"),

    # NAVIGATION (1)
    @("NV-01", "open_drafting",       "", "")
)

$allLines = @($initReq, $initNotif)
$idCounter = 200

foreach ($t in $tests) {
    $idCounter++
    $recipe = $t[1]
    $value = $t[2]
    if ($value -ne "") {
        $escapedValue = $value.Replace('\', '\').Replace('"', '\"')
        $req = "{`"jsonrpc`":`"2.0`",`"id`":$idCounter,`"method`":`"tools/call`",`"params`":{`"name`":`"topsolid_run_recipe`",`"arguments`":{`"recipe`":`"$recipe`",`"value`":`"$escapedValue`"}}}"
    } else {
        $req = "{`"jsonrpc`":`"2.0`",`"id`":$idCounter,`"method`":`"tools/call`",`"params`":{`"name`":`"topsolid_run_recipe`",`"arguments`":{`"recipe`":`"$recipe`"}}}"
    }
    $allLines += $req
}

Write-Host "Envoi de $($tests.Count) tests au serveur MCP..." -ForegroundColor Gray

$stdinBlob = $allLines -join "`n"
$allOutput = $stdinBlob | & $serverPath 2>$null
$responseLines = $allOutput -split "`n" | Where-Object { $_.Trim() -ne "" }

Write-Host "Recu $($responseLines.Count) reponses.`n" -ForegroundColor Gray

$pass = 0; $fail = 0; $warn = 0
$startIdx = 1

for ($i = 0; $i -lt $tests.Count; $i++) {
    $t = $tests[$i]
    $testId = $t[0]
    $recipe = $t[1]
    $expect = $t[3]
    $respIdx = $startIdx + $i

    if ($respIdx -ge $responseLines.Count) {
        Write-Host "  $testId $recipe : SKIP (pas de reponse)" -ForegroundColor DarkGray
        continue
    }

    $line = $responseLines[$respIdx]
    try {
        $json = $line | ConvertFrom-Json
        $text = ""
        if ($json.result -and $json.result.content) {
            foreach ($c in $json.result.content) {
                if ($c.type -eq "text") { $text = $c.text }
            }
        }
        if ($json.error) {
            Write-Host "  $testId $recipe : FAIL (error: $($json.error.message))" -ForegroundColor Red
            $fail++
        } elseif ($expect -eq "" -or $text -match [regex]::Escape($expect)) {
            $preview = if ($text.Length -gt 80) { $text.Substring(0,80) + "..." } else { $text }
            Write-Host "  $testId $recipe : PASS | $preview" -ForegroundColor Green
            $pass++
        } else {
            $preview = if ($text.Length -gt 100) { $text.Substring(0,100) + "..." } else { $text }
            Write-Host "  $testId $recipe : FAIL (expected '$expect') | $preview" -ForegroundColor Red
            $fail++
        }
    } catch {
        Write-Host "  $testId $recipe : FAIL (parse error)" -ForegroundColor Red
        $fail++
    }
}

Write-Host "`n============================================================" -ForegroundColor Yellow
Write-Host "  RESULTATS: $pass PASS / $fail FAIL / $($tests.Count) total" -ForegroundColor $(if($fail -eq 0){"Green"}else{"Red"})
Write-Host "============================================================" -ForegroundColor Yellow
