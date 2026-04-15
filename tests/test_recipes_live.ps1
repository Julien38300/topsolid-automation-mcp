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
    @("RA-01", "lire_designation",      "",    "Designation:"),
    @("RA-02", "lire_nom",              "",    "Nom:"),
    @("RA-03", "lire_reference",        "",    "Reference:"),
    @("RA-04", "lire_fabricant",        "",    "Fabricant:"),
    @("RA-05", "lire_proprietes_pdm",   "",    "Nom:"),
    @("RA-06", "lire_projet_courant",   "",    "Projet:"),
    @("RA-07", "lire_contenu_projet",   "",    "Projet:"),
    @("RA-08", "type_document",         "",    "Nom:"),
    @("RA-09", "lire_parametres",       "",    "Parametres:"),
    @("RA-10", "lire_points_3d",        "",    "Points 3D:"),
    @("RA-11", "lire_reperes_3d",       "",    "Reperes 3D:"),
    @("RA-12", "lister_esquisses",      "",    "Esquisses:"),
    @("RA-13", "lire_shapes",           "",    "Shapes:"),
    @("RA-14", "lire_operations",       "",    "Operations:"),
    @("RA-15", "detecter_assemblage",   "",    "ssemblage"),
    @("RA-16", "lister_inclusions",     "",    "Inclusions:"),
    @("RA-17", "detecter_famille",      "",    ""),
    @("RA-18", "detecter_mise_en_plan", "",    ""),
    @("RA-19", "detecter_nomenclature", "",    ""),
    @("RA-20", "lister_exporteurs",     "",    "Exporteurs:"),
    @("RA-21", "audit_piece",           "",    "AUDIT PIECE"),
    @("RA-22", "verifier_piece",        "",    "VERIFICATION QUALITE"),
    @("RA-23", "lire_masse_volume",     "",    "Volume total:"),
    @("RA-24", "lire_densite_materiau", "",    ""),
    @("RA-25", "lire_boite_englobante", "",    "Boite englobante:"),
    @("RA-26", "lister_documents_projet","",   "Documents:"),
    @("RA-27", "verifier_projet",       "",    "VERIFICATION PROJET"),
    @("RA-28", "verifier_materiaux_manquants","","VERIFICATION MATERIAUX"),
    @("RA-29", "lire_materiau",         "",    ""),
    @("RA-30", "lire_occurrences",      "",    "ccurrences:"),
    @("RA-31", "compter_pieces_assemblage","",  "COMPTAGE PIECES"),
    @("RA-32", "rapport_masse_assemblage","",  "RAPPORT MASSE"),
    @("RA-33", "attribut_lire_tout",    "",    "ATTRIBUTS"),
    @("RA-34", "attribut_lire_couleur", "",    ""),
    @("RA-35", "attribut_lire_transparence","", ""),
    @("RA-36", "attribut_lister_calques","",   "Calques:"),
    @("RA-37", "attribut_lire_couleurs_faces","",""),
    @("RA-38", "sauvegarder_document",  "",    "OK:"),
    @("RA-39", "reconstruire_document", "",    "OK:"),

    # TIER B: READ avec param
    @("RB-01", "chercher_document",     "Test", "Recherche"),
    @("RB-02", "chercher_dossier",      "Test", "Recherche dossier"),

    # TIER C: Assemblage
    @("RC-01", "audit_assemblage",      "",    "AUDIT ASSEMBLAGE"),
    @("RC-02", "detecter_assemblage",   "",    "ssemblage"),
    @("RC-03", "lister_inclusions",     "",    "Inclusions:"),
    @("RC-04", "lire_occurrences",      "",    "ccurrences:"),
    @("RC-05", "compter_pieces_assemblage","", "COMPTAGE PIECES"),
    @("RC-06", "rapport_masse_assemblage","",  "RAPPORT MASSE"),

    # TIER D: WRITE PDM
    @("RD-01", "modifier_designation",  "Test-Noemid-Recipe-Live", "OK:"),
    @("RD-02", "lire_designation",      "",    "Test-Noemid-Recipe-Live"),
    @("RD-03", "modifier_reference",    "REF-NOEMID-TEST", "OK:"),
    @("RD-04", "lire_reference",        "",    "REF-NOEMID-TEST"),
    @("RD-05", "modifier_fabricant",    "Noemid-Fabricant-Test", "OK:"),
    @("RD-06", "lire_fabricant",        "",    "Noemid-Fabricant-Test"),

    # TIER E: WRITE Attributs
    @("RE-01", "attribut_modifier_couleur_tout", "255,0,0", "OK:"),
    @("RE-02", "attribut_lire_couleur", "",    "255"),
    @("RE-03", "attribut_modifier_couleur_tout", "0,128,255", "OK:"),

    # TIER F: Export
    @("RF-01", "exporter_step",  "C:\temp\noemid_test.stp",  "OK:"),
    @("RF-02", "exporter_stl",   "C:\temp\noemid_test.stl",  "OK:"),
    @("RF-03", "exporter_iges",  "C:\temp\noemid_test.igs",  "OK:"),
    @("RF-04", "exporter_dxf",   "C:\temp\noemid_test.dxf",  ""),
    @("RF-05", "exporter_pdf",   "C:\temp\noemid_test.pdf",  "")
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
