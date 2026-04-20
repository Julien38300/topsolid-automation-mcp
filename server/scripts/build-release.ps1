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

if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null }

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
    "api-index.json",
    "help.db",                  # v1.6.0+ — SQLite FTS5 help index (5809 pages)
    "help-index-meta.json",     # v1.6.0+ — meta
    "commands-catalog.json",    # v1.6.3+ — UI commands catalog (2428 cmds)
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
    $mcpRecipes = [regex]::Matches((Get-Content $recipeFile -Raw), '\{ "([a-z_]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
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
Write-Host ""
Write-Host "=== Release prete ===" -ForegroundColor Green
Write-Host "  Zip    : $zipPath ($zipSize Mo)"
Write-Host "  Staging: $releaseDir\"
Write-Host ""
Write-Host "Pour publier sur GitHub :" -ForegroundColor Cyan
Write-Host "  gh release create v$Version `"$zipPath`" --title `"v$Version`" --notes `"Release v$Version`""
