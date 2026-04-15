#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Met a jour TopSolid MCP Server depuis GitHub Releases.
.DESCRIPTION
    Verifie la derniere version sur GitHub, telecharge et remplace les fichiers si necessaire.
    Preserve le dossier data/ (graph.json personnalise).
.EXAMPLE
    .\update.ps1            # Verifie et met a jour si necessaire
    .\update.ps1 -Force     # Force la reinstallation meme si a jour
    .\update.ps1 -Check     # Verifie seulement, sans installer
#>
param(
    [switch]$Force,
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$repo = "Julien38300/noemid-topsolid-automation"
$baseDir = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $baseDir "TopSolidMcpServer.exe"))) {
    # Script is next to the exe (release layout)
    $baseDir = $PSScriptRoot
}

$versionFile = Join-Path $baseDir "version.txt"
$currentVersion = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "0.0.0" }

Write-Host "TopSolid MCP Server - Mise a jour" -ForegroundColor Cyan
Write-Host "Version actuelle : $currentVersion"
Write-Host ""

# --- Check latest release on GitHub ---
Write-Host "Verification de la derniere version..." -NoNewline
try {
    $releaseUrl = "https://api.github.com/repos/$repo/releases/latest"
    $headers = @{ "User-Agent" = "TopSolidMcpServer-Updater" }
    $release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers -TimeoutSec 10
} catch {
    if ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host " aucune release trouvee." -ForegroundColor Yellow
        Write-Host "Le projet n'a pas encore de release GitHub."
        Write-Host "Consultez : https://github.com/$repo/releases"
        exit 0
    }
    Write-Host " erreur !" -ForegroundColor Red
    Write-Host "Impossible de contacter GitHub : $($_.Exception.Message)"
    exit 1
}

$latestVersion = $release.tag_name -replace '^v', ''
Write-Host " v$latestVersion" -ForegroundColor Green

# Compare versions (major.minor.patch)
function Compare-SemVer($a, $b) {
    $va = $a.Split('.') | ForEach-Object { [int]$_ }
    $vb = $b.Split('.') | ForEach-Object { [int]$_ }
    for ($i = 0; $i -lt 3; $i++) {
        $ai = if ($i -lt $va.Count) { $va[$i] } else { 0 }
        $bi = if ($i -lt $vb.Count) { $vb[$i] } else { 0 }
        if ($ai -lt $bi) { return -1 }
        if ($ai -gt $bi) { return 1 }
    }
    return 0
}

$cmp = Compare-SemVer $currentVersion $latestVersion
if (-not $Force -and $cmp -ge 0) {
    Write-Host "`nVous etes deja a jour (v$currentVersion) !" -ForegroundColor Green
    exit 0
}

if ($Check) {
    Write-Host "`nMise a jour disponible : v$currentVersion -> v$latestVersion" -ForegroundColor Yellow
    Write-Host "Lancez sans -Check pour installer."
    exit 0
}

Write-Host "`nMise a jour : v$currentVersion -> v$latestVersion" -ForegroundColor Yellow

# --- Find the zip asset ---
$zipAsset = $release.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
if (-not $zipAsset) {
    Write-Host "Erreur : aucun fichier .zip dans la release." -ForegroundColor Red
    Write-Host "Telechargez manuellement : $($release.html_url)"
    exit 1
}

# --- Stop running instance ---
$proc = Get-Process TopSolidMcpServer -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID }
if ($proc) {
    Write-Host "Arret de l'instance en cours (PID $($proc.Id))..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# --- Download ---
$tempDir = Join-Path $env:TEMP "TopSolidMcpServer_update"
$tempZip = Join-Path $env:TEMP "TopSolidMcpServer_update.zip"

if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
if (Test-Path $tempZip) { Remove-Item $tempZip -Force }

Write-Host "Telechargement ($([math]::Round($zipAsset.size / 1MB, 1)) Mo)..."
Invoke-WebRequest -Uri $zipAsset.browser_download_url -OutFile $tempZip -Headers $headers

# --- Extract ---
Write-Host "Extraction..."
Expand-Archive -Path $tempZip -DestinationPath $tempDir -Force

# Find the actual content folder (might be nested)
$extractedContent = $tempDir
$subdirs = Get-ChildItem $tempDir -Directory
if ($subdirs.Count -eq 1 -and (Test-Path (Join-Path $subdirs[0].FullName "TopSolidMcpServer.exe"))) {
    $extractedContent = $subdirs[0].FullName
}

# --- Backup current data/ if customized ---
$dataDir = Join-Path $baseDir "data"
$backupDir = Join-Path $baseDir "data_backup"
if (Test-Path $dataDir) {
    Write-Host "Sauvegarde de data/..."
    if (Test-Path $backupDir) { Remove-Item $backupDir -Recurse -Force }
    Copy-Item $dataDir $backupDir -Recurse
}

# --- Copy new files (overwrite exe, dlls, scripts) ---
Write-Host "Installation des nouveaux fichiers..."
$filesToCopy = Get-ChildItem $extractedContent -File
foreach ($file in $filesToCopy) {
    Copy-Item $file.FullName (Join-Path $baseDir $file.Name) -Force
}

# Copy data/ from update (new graph.json etc.)
$newDataDir = Join-Path $extractedContent "data"
if (Test-Path $newDataDir) {
    if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir -Force | Out-Null }
    Get-ChildItem $newDataDir -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $dataDir $_.Name) -Force
    }
}

# --- Update version.txt ---
Set-Content -Path $versionFile -Value $latestVersion -NoNewline

# --- Cleanup ---
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue

# --- Show changelog ---
Write-Host ""
Write-Host "=== Mise a jour terminee ! ===" -ForegroundColor Green
Write-Host "Version : v$latestVersion"
if ($release.body) {
    Write-Host ""
    Write-Host "--- Changelog ---" -ForegroundColor Cyan
    Write-Host $release.body
}
Write-Host ""
Write-Host "Relancez votre client IA pour utiliser la nouvelle version."
