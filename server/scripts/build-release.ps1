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

$dataFiles = @("graph.json", "api-index.json")
foreach ($file in $dataFiles) {
    $src = Join-Path $dataDir $file
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $relDataDir $file) -Force
        Write-Host "  + data/$file" -ForegroundColor Gray
    }
}

# version.txt
Set-Content -Path (Join-Path $releaseDir "version.txt") -Value $Version -NoNewline
Write-Host "  + version.txt ($Version)" -ForegroundColor Gray

# --- Verify skill sync ---
Write-Host ""
Write-Host "Verification sync skills..." -ForegroundColor Yellow
$recipeFile = Join-Path $srcDir "Tools\RecipeTool.cs"
$skillFile = Join-Path (Split-Path (Split-Path $projectRoot)) "noemid-topsolid\skills\topsolid-mcp\SKILL.md"
$openclawFile = Join-Path $env:USERPROFILE ".openclaw\agents\topsolid\system.md"

if (Test-Path $recipeFile) {
    $mcpRecipes = [regex]::Matches((Get-Content $recipeFile -Raw), '\{ "([a-z_]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    $mcpCount = $mcpRecipes.Count
    Write-Host "  MCP: $mcpCount recettes" -ForegroundColor Gray

    $missingSkill = @()
    $missingOC = @()

    if (Test-Path $skillFile) {
        $skillContent = Get-Content $skillFile -Raw
        foreach ($r in $mcpRecipes) {
            if ($skillContent -notmatch [regex]::Escape($r)) { $missingSkill += $r }
        }
        if ($missingSkill.Count -gt 0) {
            Write-Host "  ATTENTION: $($missingSkill.Count) recettes manquantes dans Hermes SKILL.md !" -ForegroundColor Red
            $missingSkill | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        } else {
            Write-Host "  Hermes SKILL.md: OK ($mcpCount/$mcpCount)" -ForegroundColor Green
        }
    } else {
        Write-Host "  Hermes SKILL.md: non trouve ($skillFile)" -ForegroundColor Yellow
    }

    if (Test-Path $openclawFile) {
        $ocContent = Get-Content $openclawFile -Raw
        foreach ($r in $mcpRecipes) {
            if ($ocContent -notmatch [regex]::Escape($r)) { $missingOC += $r }
        }
        if ($missingOC.Count -gt 0) {
            Write-Host "  ATTENTION: $($missingOC.Count) recettes manquantes dans OpenClaw system.md !" -ForegroundColor Red
            $missingOC | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
        } else {
            Write-Host "  OpenClaw system.md: OK ($mcpCount/$mcpCount)" -ForegroundColor Green
        }
    } else {
        Write-Host "  OpenClaw system.md: non trouve" -ForegroundColor Yellow
    }
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
