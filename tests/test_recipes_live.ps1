$serverPath = Join-Path $PSScriptRoot "..\src\bin\Debug\net48\TopSolidMcpServer.exe"

Write-Host "============================================================" -ForegroundColor Yellow
Write-Host "  TESTS LIVE RECETTES - TopSolid MCP RecipeTool" -ForegroundColor Yellow
Write-Host "  Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow

# Build all requests as a single stdin blob
$initReq = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"RecipeTestClient","version":"1.0"}}}'
$initNotif = '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}'

# Test definitions: id, recipe, value, expectContains
$tests = @(
    # TIER A: READ sans param
    @("RA-01", "read_designation",      "",    "Designation:"),
    @("RA-02", "read_name",              "",    "Nom:"),
    @("RA-03", "read_reference",        "",    "Reference:"),
    @("RA-04", "read_manufacturer",        "",    "Fabricant:"),
    @("RA-05", "read_pdm_properties",   "",    "Nom:"),
    @("RA-06", "read_current_project",   "",    "Projet:"),
    @("RA-07", "read_project_contents",   "",    "Projet:"),
    @("RA-08", "document_type",         "",    "Nom:"),
    @("RA-09", "read_parameters",       "",    "Parametres:"),
    @("RA-10", "read_3d_points",        "",    "Points 3D:"),
    @("RA-11", "read_3d_frames",       "",    "Reperes 3D:"),
    @("RA-12", "list_sketches",      "",    "Esquisses:"),
    @("RA-13", "read_shapes",           "",    "Shapes:"),
    @("RA-14", "read_operations",       "",    "Operations:"),
    @("RA-15", "detect_assembly",   "",    "ssemblage"),
    @("RA-16", "list_inclusions",     "",    "Inclusions:"),
    @("RA-17", "detect_family",      "",    ""),
    @("RA-18", "detect_drafting", "",    ""),
    @("RA-19", "detect_bom", "",    ""),
    @("RA-20", "list_exporters",     "",    "Exporteurs:"),
    @("RA-21", "audit_part",           "",    "AUDIT PIECE"),
    @("RA-22", "check_part",        "",    "VERIFICATION QUALITE"),
    @("RA-23", "read_mass_volume",     "",    "Volume total:"),
    @("RA-24", "read_material_density", "",    ""),
    @("RA-25", "read_bounding_box", "",    "Boite englobante:"),
    @("RA-26", "list_project_documents","",   "Documents:"),
    @("RA-27", "check_project",       "",    "VERIFICATION PROJET"),
    @("RA-28", "check_missing_materials","","VERIFICATION MATERIAUX"),
    @("RA-29", "read_material",         "",    ""),
    @("RA-30", "read_occurrences",      "",    "ccurrences:"),
    @("RA-31", "count_assembly_parts","",  "COMPTAGE PIECES"),
    @("RA-32", "assembly_mass_report","",  "RAPPORT MASSE"),
    @("RA-33", "attr_read_all",    "",    "ATTRIBUTS"),
    @("RA-34", "attr_read_color", "",    ""),
    @("RA-35", "attr_read_transparency","", ""),
    @("RA-36", "attr_list_layers","",   "Calques:"),
    @("RA-37", "attr_read_face_colors","",""),
    @("RA-38", "save_document",  "",    "OK:"),
    @("RA-39", "rebuild_document", "",    "OK:"),

    # TIER B: READ avec param
    @("RB-01", "search_document",     "Test", "Recherche"),
    @("RB-02", "search_folder",      "Test", "Recherche dossier"),

    # TIER C: Assemblage
    @("RC-01", "audit_assembly",      "",    "AUDIT ASSEMBLAGE"),
    @("RC-02", "detect_assembly",   "",    "ssemblage"),
    @("RC-03", "list_inclusions",     "",    "Inclusions:"),
    @("RC-04", "read_occurrences",      "",    "ccurrences:"),
    @("RC-05", "count_assembly_parts","", "COMPTAGE PIECES"),
    @("RC-06", "assembly_mass_report","",  "RAPPORT MASSE"),

    # TIER D: WRITE PDM
    @("RD-01", "set_designation",  "Test-Noemid-Recipe-Live", "OK:"),
    @("RD-02", "read_designation",      "",    "Test-Noemid-Recipe-Live"),
    @("RD-03", "set_reference",    "REF-NOEMID-TEST", "OK:"),
    @("RD-04", "read_reference",        "",    "REF-NOEMID-TEST"),
    @("RD-05", "set_manufacturer",    "Noemid-Fabricant-Test", "OK:"),
    @("RD-06", "read_manufacturer",        "",    "Noemid-Fabricant-Test"),

    # TIER E: WRITE Attributs
    @("RE-01", "attr_set_color_all", "255,0,0", "OK:"),
    @("RE-02", "attr_read_color", "",    "255"),
    @("RE-03", "attr_set_color_all", "0,128,255", "OK:"),

    # TIER F: Export
    @("RF-01", "export_step",  "C:\temp\noemid_test.stp",  "OK:"),
    @("RF-02", "export_stl",   "C:\temp\noemid_test.stl",  "OK:"),
    @("RF-03", "export_iges",  "C:\temp\noemid_test.igs",  "OK:"),
    @("RF-04", "export_dxf",   "C:\temp\noemid_test.dxf",  ""),
    @("RF-05", "export_pdf",   "C:\temp\noemid_test.pdf",  "")
)

# Build all JSON-RPC requests
$allLines = @($initReq, $initNotif)
$idCounter = 100

foreach ($t in $tests) {
    $idCounter++
    $recipe = $t[1]
    $value = $t[2]
    if ($value -ne "") {
        $escapedValue = $value.Replace('\', '\\').Replace('"', '\"')
        $req = "{`"jsonrpc`":`"2.0`",`"id`":$idCounter,`"method`":`"tools/call`",`"params`":{`"name`":`"topsolid_run_recipe`",`"arguments`":{`"recipe`":`"$recipe`",`"value`":`"$escapedValue`"}}}"
    } else {
        $req = "{`"jsonrpc`":`"2.0`",`"id`":$idCounter,`"method`":`"tools/call`",`"params`":{`"name`":`"topsolid_run_recipe`",`"arguments`":{`"recipe`":`"$recipe`"}}}"
    }
    $allLines += $req
}

Write-Host "Envoi de $($tests.Count) tests au serveur MCP..." -ForegroundColor Gray

# Ensure C:\temp exists for export tests
if (-not (Test-Path "C:\temp")) { New-Item -ItemType Directory -Path "C:\temp" -Force | Out-Null }

# Pipe everything to MCP server, collect output
$stdinBlob = $allLines -join "`n"
$allOutput = $stdinBlob | & $serverPath 2>$null
$responseLines = $allOutput -split "`n" | Where-Object { $_.Trim() -ne "" }

Write-Host "Recu $($responseLines.Count) reponses.`n" -ForegroundColor Gray

# Parse results: first response is init, then one per test
$pass = 0; $fail = 0
$results = @()

# Skip init response (first line)
$startIdx = 1  # response[0] = init response
if ($responseLines.Count -le 1) {
    Write-Host "ERREUR: Aucune reponse du serveur!" -ForegroundColor Red
    if ($responseLines.Count -gt 0) { Write-Host $responseLines[0] -ForegroundColor Gray }
    exit 1
}

$currentTier = ""
for ($i = 0; $i -lt $tests.Count; $i++) {
    $t = $tests[$i]
    $id = $t[0]
    $recipe = $t[1]
    $value = $t[2]
    $expect = $t[3]

    # Print tier header
    $tier = $id.Substring(0, 2)
    if ($tier -ne $currentTier) {
        $currentTier = $tier
        $tierName = switch ($tier) {
            "RA" { "TIER A: READ sans param" }
            "RB" { "TIER B: READ avec param" }
            "RC" { "TIER C: Assemblage" }
            "RD" { "TIER D: WRITE PDM" }
            "RE" { "TIER E: WRITE Attributs" }
            "RF" { "TIER F: Export" }
            default { $tier }
        }
        Write-Host "`n===== $tierName =====" -ForegroundColor Yellow
    }

    $label = if ($value -ne "") { "$recipe ($value)" } else { $recipe }

    $responseIdx = $startIdx + $i
    $ok = $true
    $reason = ""
    $text = ""

    if ($responseIdx -ge $responseLines.Count) {
        $ok = $false
        $reason = "NO RESPONSE"
    } else {
        $line = $responseLines[$responseIdx]
        if ($line -match '"text"\s*:\s*"((?:[^"\\]|\\.)*)') {
            $text = $matches[1] -replace '\\n', "`n" -replace '\\"', '"' -replace '\\\\', '\'
            if ($text -match "Erreur de compilation|erreur de compilation") {
                $ok = $false
                $reason = "COMPILE"
            } elseif ($expect -ne "" -and -not $text.Contains($expect)) {
                $ok = $false
                $reason = "MISSING: $expect"
            }
        } elseif ($line -match '"error"') {
            $ok = $false
            $reason = "MCP ERROR"
            $text = $line
        } else {
            $ok = $false
            $reason = "PARSE"
            $text = $line
        }
    }

    $short = ($text -replace "`n", " | ")
    if ($short.Length -gt 100) { $short = $short.Substring(0, 100) + "..." }

    if ($ok) {
        Write-Host "  PASS [$id] $label" -ForegroundColor Green -NoNewline
        Write-Host "  $short" -ForegroundColor DarkGray
        $pass++
    } else {
        Write-Host "  FAIL [$id] $label [$reason]" -ForegroundColor Red -NoNewline
        Write-Host "  $short" -ForegroundColor DarkGray
        $fail++
    }

    $results += [PSCustomObject]@{
        Id = $id; Recipe = $recipe; Value = $value
        Status = if ($ok) { "PASS" } else { "FAIL" }
        Reason = $reason; Response = $text
    }
}

# Summary
Write-Host "`n============================================================" -ForegroundColor Yellow
$color = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "  RESULTATS: $pass PASS / $fail FAIL / $($pass + $fail) TOTAL" -ForegroundColor $color
Write-Host "============================================================" -ForegroundColor Yellow

if ($fail -gt 0) {
    Write-Host "`nDETAIL FAILS:" -ForegroundColor Red
    foreach ($r in $results) {
        if ($r.Status -eq "FAIL") {
            $s = ($r.Response -replace "`n", " | ")
            if ($s.Length -gt 150) { $s = $s.Substring(0, 150) + "..." }
            Write-Host "  [$($r.Id)] $($r.Recipe) - $($r.Reason): $s" -ForegroundColor Red
        }
    }
}

# Save
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputDir = Join-Path $PSScriptRoot "TestResults"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }
$outputFile = Join-Path $outputDir "recipes_$timestamp.json"
$results | ConvertTo-Json -Depth 3 | Set-Content -Path $outputFile -Encoding UTF8
Write-Host "`nResultats: $outputFile" -ForegroundColor Gray
