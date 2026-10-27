#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build TopSolid MCP Server, scrub build-machine paths, leak-scan, and package for release.
.DESCRIPTION
    Compile le projet en Release, copie dans release-staging/, nettoie les chemins PDB RSDS
    et settings.json, verifie l'absence de fuite (Noemid_System / jpoir), et genere le zip
    + SHA256SUMS.txt (LF ASCII) prets a publier sur GitHub Releases.
.EXAMPLE
    .\scripts\build-release.ps1
    .\scripts\build-release.ps1 -Version 1.8.10
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
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

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

$updateScript = Join-Path $projectRoot "scripts\update.ps1"
if (Test-Path $updateScript) {
    Copy-Item $updateScript (Join-Path $releaseDir "update.ps1") -Force
    Write-Host "  + update.ps1" -ForegroundColor Gray
}

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
    "help.db",
    "help-index-meta.json",
    "commands-catalog.json",
    "commands-api-links.json",
    "recipe-list.txt"
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

# Ship Microsoft.Data.Sqlite + SQLitePCLRaw DLLs
$binDir = Join-Path $srcDir "bin\Release\net48"
$sqliteDlls = @(Get-ChildItem $binDir -EA SilentlyContinue | Where-Object {
    $_.Name -like "*Sqlite*.dll" -or $_.Name -like "*SQLite*.dll"
})
foreach ($dll in $sqliteDlls) {
    Copy-Item $dll.FullName (Join-Path $releaseDir $dll.Name) -Force
    Write-Host "  + $($dll.Name)" -ForegroundColor Gray
}

$systemDlls = @("System.Buffers.dll","System.Memory.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll")
foreach ($name in $systemDlls) {
    $src = Join-Path $binDir $name
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $releaseDir $name) -Force
        Write-Host "  + $name" -ForegroundColor Gray
    }
}

$runtimesSrc = Join-Path $binDir "runtimes"
if (Test-Path $runtimesSrc) {
    $runtimesDst = Join-Path $releaseDir "runtimes"
    if (Test-Path $runtimesDst) { Remove-Item $runtimesDst -Recurse -Force }
    Copy-Item $runtimesSrc $runtimesDst -Recurse -Force
    $archCount = (Get-ChildItem $runtimesDst -Directory).Count
    Write-Host "  + runtimes/ ($archCount OS/arch - native e_sqlite3)" -ForegroundColor Gray
}

Set-Content -Path (Join-Path $releaseDir "version.txt") -Value $Version -NoNewline
Write-Host "  + version.txt ($Version)" -ForegroundColor Gray

# --- Noob install: ship install/ ---
$projectRootAll = Split-Path -Parent $projectRoot
foreach ($dir in @("install")) {
    $srcDirShip = Join-Path $projectRootAll $dir
    if (Test-Path $srcDirShip) {
        Copy-Item $srcDirShip (Join-Path $releaseDir $dir) -Recurse -Force
        Write-Host "  + $dir/ (noob install)" -ForegroundColor Gray
    }
}

# Ensure no build-machine settings.json (DPAPI key blob) is shipped
$stagingSettings = Join-Path $releaseDir "settings.json"
if (Test-Path $stagingSettings) { Remove-Item $stagingSettings -Force }

# --- Scrub build-machine PDB paths (RSDS) in staged binaries ---
$needleStr = "N:\Noemid_System\Projets\topsolid-automation-mcp"
$replacementStr = "C:\topsolid-automation-mcp                      "
$needleBytes = [System.Text.Encoding]::ASCII.GetBytes($needleStr)
$replacementBytes = [System.Text.Encoding]::ASCII.GetBytes($replacementStr)
if ($needleBytes.Length -ne $replacementBytes.Length) {
    throw "PDB scrub byte length mismatch ($($needleBytes.Length) vs $($replacementBytes.Length))"
}
foreach ($binName in @("TopSolidMcpServer.exe", "TopSolidApiGraph.Core.dll")) {
    $binPath = Join-Path $releaseDir $binName
    if (Test-Path $binPath) {
        $bytes = [System.IO.File]::ReadAllBytes($binPath)
        $patched = 0
        for ($i = 0; $i -le $bytes.Length - $needleBytes.Length; $i++) {
            $match = $true
            for ($j = 0; $j -lt $needleBytes.Length; $j++) {
                if ($bytes[$i + $j] -ne $needleBytes[$j]) { $match = $false; break }
            }
            if ($match) {
                [System.Array]::Copy($replacementBytes, 0, $bytes, $i, $replacementBytes.Length)
                $patched++
                $i += $needleBytes.Length - 1
            }
        }
        if ($patched -gt 0) {
            [System.IO.File]::WriteAllBytes($binPath, $bytes)
            Write-Host "  ~ Scrubbed $patched PDB path(s) in $binName" -ForegroundColor Gray
        }
    }
}

# --- Leak scan on release-staging ---
$leakPatterns = @("Noemid_System", "jpoir")
foreach ($fileObj in Get-ChildItem $releaseDir -Recurse -File) {
    $rawText = [System.IO.File]::ReadAllText($fileObj.FullName, [System.Text.Encoding]::GetEncoding("ISO-8859-1"))
    foreach ($pat in $leakPatterns) {
        if ($rawText.IndexOf($pat, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Write-Host "ERREUR: Fuite detectee ('$pat') dans $($fileObj.FullName)" -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host "  OK Leak scan staging (0 fuite)" -ForegroundColor Green

$installReadme = Join-Path $projectRootAll "install\README.md"
if (-not (Test-Path $installReadme)) {
    Write-Host "  ! install/README.md manquant - a rediger avant release" -ForegroundColor Yellow
}

Write-Host ""
$recipeFile = Join-Path $srcDir "Tools\RecipeTool.cs"
if (Test-Path $recipeFile) {
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

# --- SHA256SUMS.txt, published next to the zip as a release asset (LF ASCII) ---
$zipHash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sumsName = "SHA256SUMS.txt"
$sumsPath = Join-Path $projectRoot $sumsName
[System.IO.File]::WriteAllText($sumsPath, $zipHash + "  " + $zipName + "`n", [System.Text.Encoding]::ASCII)

Write-Host ""
Write-Host "=== Release prete ===" -ForegroundColor Green
Write-Host "  Zip    : $zipPath ($zipSize Mo)"
Write-Host "  SHA256 : $zipHash"
Write-Host "  Sums   : $sumsPath"
Write-Host "  Staging: $releaseDir\"
Write-Host ""
Write-Host "Pour publier sur GitHub Releases :" -ForegroundColor Cyan
Write-Host "  python server\scripts\publish-release.py --version $Version --title `"v$Version`" --notes-file release-notes.md"
