#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build TopSolid MCP Server and package for release.
.DESCRIPTION
    Compile le projet, copie dans release-staging/, genere le zip pret a upload sur GitHub Releases.
.EXAMPLE
    .\scripts\build-release.ps1                # Build + package
    .\scripts\build-release.ps1 -Version 1.1.0 # Avec un numero de version specifique
#>
param(
    [string]$Version
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$srcDir = Join-Path $projectRoot "src"
$csproj = Join-Path $srcDir "TopSolidMcpServer.csproj"
$releaseDir = Join-Path $projectRoot "release-staging"
$dataDir = Join-Path $projectRoot "data"

# --- Determine version ---
if (-not $Version) {
    # Read from .csproj
    [xml]$proj = Get-Content $csproj
    $Version = $proj.Project.PropertyGroup.Version
    if (-not $Version) { $Version = "1.0.0" }
}

Write-Host "=== TopSolid MCP Server - Build Release ===" -ForegroundColor Cyan
Write-Host "Version: $Version"
Write-Host ""

# --- Update version in .csproj ---
[xml]$proj = Get-Content $csproj
$versionNode = $proj.Project.PropertyGroup.SelectSingleNode("Version")
if ($versionNode) {
    $versionNode.InnerText = $Version
} else {
    $newNode = $proj.CreateElement("Version")
    $newNode.InnerText = $Version
    $proj.Project.PropertyGroup.AppendChild($newNode) | Out-Null
}
$proj.Save($csproj)

# --- Build ---
Write-Host "Build en cours..." -ForegroundColor Yellow
$buildOutput = Join-Path $srcDir "bin\Release\net48"
dotnet build $csproj -c Release -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERREUR: Build echoue !" -ForegroundColor Red
    exit 1
}
Write-Host "Build OK" -ForegroundColor Green

# --- Prepare release-staging ---
Write-Host "Preparation de release-staging/..."

# Wipe the staging folder first. Everything below is regenerated from the build
# output and from server/data/, so a leftover from a previous build is pure
# noise: a file dropped from $dataFiles (api-index.json) or renamed in the
# build output would otherwise survive here and be zipped again.
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

# Core files from build output
$coreFiles = @(
    "TopSolidMcpServer.exe",
    "TopSolidMcpServer.exe.config",
    "Newtonsoft.Json.dll",
    "TopSolidApiGraph.Core.dll",
    "topsolid-mcp.ico"
)

foreach ($file in $coreFiles) {
    $src = Join-Path $buildOutput $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $releaseDir $file) -Force
        Write-Host "  + $file" -ForegroundColor Gray
    } else {
        Write-Host "  ! $file introuvable dans $buildOutput" -ForegroundColor Yellow
    }
}

# Copy update.ps1
$updateScript = Join-Path $projectRoot "scripts\update.ps1"
if (Test-Path $updateScript) {
    Copy-Item $updateScript (Join-Path $releaseDir "update.ps1") -Force
    Write-Host "  + update.ps1" -ForegroundColor Gray
}

# Copy ico file from src if not in build output
$icoSrc = Join-Path $srcDir "topsolid-mcp.ico"
$icoDst = Join-Path $releaseDir "topsolid-mcp.ico"
if (-not (Test-Path $icoDst) -and (Test-Path $icoSrc)) {
    Copy-Item $icoSrc $icoDst -Force
    Write-Host "  + topsolid-mcp.ico (from src)" -ForegroundColor Gray
}

# data/ subfolder
$relDataDir = Join-Path $releaseDir "data"
if (-not (Test-Path $relDataDir)) { New-Item -ItemType Directory -Path $relDataDir -Force | Out-Null }

$dataFiles = @(
    "graph.json",
    "help.db",                  # v1.6.0+ — SQLite FTS5 help index (5809 pages)
    "help-index-meta.json",     # v1.6.0+ — meta
    "commands-catalog.json",    # v1.6.3+ — UI commands catalog (2428 cmds)
    "commands-api-links.json",  # v1.6.5+ - UI command -> API links, read by SearchCommandsTool
    "recipe-list.txt"           # recipe manifest (reference)
)
foreach ($file in $dataFiles) {
    $src = Join-Path $dataDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $relDataDir $file) -Force
        $sz = [math]::Round((Get-Item $src).Length / 1KB, 1)
        Write-Host "  + data/$file ($sz KB)" -ForegroundColor Gray
    } else {
        Write-Host "  - data/$file skipped (not found)" -ForegroundColor DarkGray
    }
}
# Also ship Microsoft.Data.Sqlite + SQLitePCLRaw DLLs (required at runtime for help.db)
$binDir = Join-Path $srcDir "bin\Release\net48"
$sqliteDlls = @(Get-ChildItem $binDir -EA SilentlyContinue | Where-Object {
    $_.Name -like "*Sqlite*.dll" -or $_.Name -like "*SQLite*.dll"
})
foreach ($dll in $sqliteDlls) {
    Copy-Item $dll.FullName (Join-Path $releaseDir $dll.Name) -Force
    Write-Host "  + $($dll.Name)" -ForegroundColor Gray
}

# System.* support DLLs required by Microsoft.Data.Sqlite on net48
$systemDlls = @("System.Buffers.dll","System.Memory.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll")
foreach ($name in $systemDlls) {
    $src = Join-Path $binDir $name
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $releaseDir $name) -Force
        Write-Host "  + $name" -ForegroundColor Gray
    }
}

# runtimes/ subtree (native e_sqlite3.dll per OS/arch)
$runtimesSrc = Join-Path $binDir "runtimes"
if (Test-Path $runtimesSrc) {
    $runtimesDst = Join-Path $releaseDir "runtimes"
    if (Test-Path $runtimesDst) { Remove-Item $runtimesDst -Recurse -Force }
    Copy-Item $runtimesSrc $runtimesDst -Recurse -Force
    $archCount = (Get-ChildItem $runtimesDst -Directory).Count
    Write-Host "  + runtimes/ ($archCount OS/arch - native e_sqlite3)" -ForegroundColor Gray
}

# version.txt
Set-Content -Path (Join-Path $releaseDir "version.txt") -Value $Version -NoNewline
Write-Host "  + version.txt ($Version)" -ForegroundColor Gray

# --- Recipe count summary (informational) ---
Write-Host ""
$recipeFile = Join-Path $srcDir "Tools\RecipeTool.cs"
if (Test-Path $recipeFile) {
    # Recipe keys may contain digits (read_3d_points, select_3d_point), so the
    # character class must include 0-9. Anchoring on the R/RW/RD factory call
    # also keeps unrelated dictionary literals out of the count.
    $mcpRecipes = [regex]::Matches((Get-Content $recipeFile -Raw), '\{ "([a-z0-9_]+)", R') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    Write-Host "Packaged recipes: $($mcpRecipes.Count)" -ForegroundColor Gray
}

# --- Create zip ---
$zipName = "TopSolidMcpServer-v$Version.zip"
$zipPath = Join-Path $projectRoot $zipName

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host ""
Write-Host "Creation du zip..."
Compress-Archive -Path "$releaseDir\*" -DestinationPath $zipPath -Force

$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)

# --- SHA256SUMS.txt, published next to the zip as a release asset ---
# Format is the one sha256sum(1) writes and reads back: "<lowercase hash>  <file name>".
# scripts/update.ps1 downloads this asset and refuses to install a zip whose hash
# does not match, so the file must ship with every release.
$zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sumsName = "SHA256SUMS.txt"
$sumsPath = Join-Path $projectRoot $sumsName
# Written with a single LF, not CRLF: "sha256sum -c SHA256SUMS.txt" on Linux takes
# everything up to the line terminator as the file name, so a CR would make it look
# for a name ending in a carriage return and report the file as missing. Get-Content in update.ps1 splits on
# LF as well as CRLF, so the Windows side is unaffected.
[System.IO.File]::WriteAllText($sumsPath, $zipHash + "  " + $zipName + "`n", [System.Text.Encoding]::ASCII)
Write-Host ""
Write-Host "=== Release prete ===" -ForegroundColor Green
Write-Host "  Zip    : $zipPath ($zipSize Mo)"
Write-Host "  SHA256 : $zipHash"
Write-Host "  Sums   : $sumsPath"
Write-Host "  Staging: $releaseDir\"
Write-Host ""
Write-Host "Pour publier sur GitHub :" -ForegroundColor Cyan
Write-Host "  gh release create v$Version `"$zipPath`" `"$sumsPath`" --title `"v$Version`" --notes `"Release v$Version`""
