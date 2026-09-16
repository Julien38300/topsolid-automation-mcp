#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates TopSolid MCP Server from GitHub Releases.
.DESCRIPTION
    Checks the latest release on GitHub, downloads the zip, verifies it against the
    SHA256SUMS.txt asset published with the release, then replaces the whole
    installation: the executable, the DLLs, data/ and runtimes/.

    data/ is NOT preserved. It is overwritten by the data/ shipped in the release.
    The previous data/ is copied to data_backup/ next to the executable before the
    install, so a customised graph.json can be restored from there by hand.

    The install is aborted when the downloaded zip does not match the published
    hash, and also when the release publishes no SHA256SUMS.txt at all (releases
    made before this check existed). Pass -AllowUnverified to install anyway.
.EXAMPLE
    .\update.ps1                   # Check and update if needed
    .\update.ps1 -Force            # Reinstall even when already up to date
    .\update.ps1 -Check            # Check only, install nothing
    .\update.ps1 -AllowUnverified  # Install a release that ships no SHA256SUMS.txt
#>
param(
    [switch]$Force,
    [switch]$Check,
    [switch]$AllowUnverified
)

$ErrorActionPreference = "Stop"
$repo = "Julien38300/topsolid-automation-mcp"
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

# Splits "1.7.0-beta.2+build5" into three numeric core segments and a pre-release
# label. Non-numeric text is tolerated: [int]"0-beta" used to throw, and under
# $ErrorActionPreference = "Stop" a pre-release tag killed the updater instead of
# being reported as a newer version.
function ConvertTo-SemVerParts([string]$v) {
    if ([string]::IsNullOrWhiteSpace($v)) { $v = "0.0.0" }
    $v = $v.Trim() -replace '^[vV]', ''
    $v = @($v -split '\+', 2)[0]          # drop build metadata
    $split = @($v -split '-', 2)
    $core = $split[0]
    $pre = ''
    if ($split.Count -gt 1) { $pre = $split[1] }
    $numbers = @(0, 0, 0)
    $segments = @($core -split '\.')
    for ($i = 0; $i -lt 3 -and $i -lt $segments.Count; $i++) {
        if ($segments[$i] -match '^(\d+)') { $numbers[$i] = [int]$matches[1] }
    }
    return [PSCustomObject]@{ Numbers = $numbers; PreRelease = $pre }
}

# Compares two versions. Returns -1, 0 or 1.
# Pre-release ordering follows semver on the rule that matters here: 1.7.0-beta is
# older than 1.7.0. Two pre-release labels are compared as plain strings, which is
# an approximation of the semver identifier rules.
function Compare-SemVer($a, $b) {
    $pa = ConvertTo-SemVerParts $a
    $pb = ConvertTo-SemVerParts $b
    for ($i = 0; $i -lt 3; $i++) {
        if ($pa.Numbers[$i] -lt $pb.Numbers[$i]) { return -1 }
        if ($pa.Numbers[$i] -gt $pb.Numbers[$i]) { return 1 }
    }
    if ($pa.PreRelease -eq $pb.PreRelease) { return 0 }
    if ($pa.PreRelease -eq '') { return 1 }    # a release outranks its pre-releases
    if ($pb.PreRelease -eq '') { return -1 }
    $ordinal = [string]::CompareOrdinal($pa.PreRelease, $pb.PreRelease)
    if ($ordinal -lt 0) { return -1 }
    if ($ordinal -gt 0) { return 1 }
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

# --- Download ---
$tempDir = Join-Path $env:TEMP "TopSolidMcpServer_update"
$tempZip = Join-Path $env:TEMP "TopSolidMcpServer_update.zip"

if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
if (Test-Path $tempZip) { Remove-Item $tempZip -Force }

Write-Host "Telechargement ($([math]::Round($zipAsset.size / 1MB, 1)) Mo)..."
Invoke-WebRequest -Uri $zipAsset.browser_download_url -OutFile $tempZip -Headers $headers

# --- Verify the download against the SHA256SUMS.txt asset of the release ---
# Without this the updater overwrote TopSolidMcpServer.exe with whatever the
# download happened to return. Any mismatch aborts before a single file is
# replaced. scripts/build-release.ps1 produces the SHA256SUMS.txt to upload.
$sumsAsset = $release.assets | Where-Object { $_.name -eq "SHA256SUMS.txt" } | Select-Object -First 1
if (-not $sumsAsset) {
    $sumsAsset = $release.assets | Where-Object { $_.name -like "*SHA256SUMS*" } | Select-Object -First 1
}

if ($sumsAsset) {
    Write-Host "Verification de l'empreinte SHA-256..." -NoNewline
    $tempSums = Join-Path $env:TEMP "TopSolidMcpServer_update.sha256"
    if (Test-Path $tempSums) { Remove-Item $tempSums -Force }
    Invoke-WebRequest -Uri $sumsAsset.browser_download_url -OutFile $tempSums -Headers $headers

    # sha256sum layout: "<hash>  <file name>", the optional "*" marks a binary read.
    $expectedHash = $null
    $firstHash = $null
    $entryCount = 0
    foreach ($line in (Get-Content $tempSums)) {
        if ($line -match '^\s*([0-9a-fA-F]{64})\s+\*?(.+?)\s*$') {
            $entryCount++
            $lineHash = $matches[1].ToLowerInvariant()
            $lineName = Split-Path -Leaf $matches[2]
            if ($entryCount -eq 1) { $firstHash = $lineHash }
            if ($lineName -eq $zipAsset.name) { $expectedHash = $lineHash; break }
        }
    }
    if (-not $expectedHash -and $entryCount -eq 1) { $expectedHash = $firstHash }
    Remove-Item $tempSums -Force -ErrorAction SilentlyContinue

    if (-not $expectedHash) {
        Write-Host " impossible !" -ForegroundColor Red
        Write-Host "SHA256SUMS.txt ne contient aucune empreinte pour $($zipAsset.name)."
        Write-Host "Abandon : aucun fichier n'a ete remplace."
        Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
        exit 1
    }

    $actualHash = (Get-FileHash $tempZip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        Write-Host " ECHEC !" -ForegroundColor Red
        Write-Host "  Attendu : $expectedHash"
        Write-Host "  Obtenu  : $actualHash"
        Write-Host "Le fichier telecharge ne correspond pas a la release."
        Write-Host "Abandon : aucun fichier n'a ete remplace."
        Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host " OK" -ForegroundColor Green
} elseif ($AllowUnverified) {
    Write-Host "Aucun SHA256SUMS.txt dans cette release - verification ignoree (-AllowUnverified)." -ForegroundColor Yellow
} else {
    Write-Host "Erreur : cette release ne publie pas de SHA256SUMS.txt." -ForegroundColor Red
    Write-Host "L'integrite du telechargement ne peut pas etre verifiee. Abandon."
    Write-Host "Relancez avec -AllowUnverified pour installer sans verification,"
    Write-Host "ou telechargez manuellement : $($release.html_url)"
    Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
    exit 1
}

# --- Stop running instance (only once the archive is known to be genuine) ---
$proc = Get-Process TopSolidMcpServer -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $PID }
if ($proc) {
    Write-Host "Arret de l'instance en cours (PID $($proc.Id))..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 2
}

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

# --- Copy the extracted tree over the installation, subfolders included ---
# Only top-level files plus data/ used to be copied. runtimes/ - which carries the
# native e_sqlite3.dll that Microsoft.Data.Sqlite loads to open help.db - was never
# refreshed, so a SQLitePCLRaw bump broke help search on every installation that had
# gone through this updater. Walking the whole tree keeps any future subfolder in sync.
Write-Host "Installation des nouveaux fichiers..."
$srcRoot = (Resolve-Path $extractedContent).Path.TrimEnd('\')
$failed = @()
foreach ($file in (Get-ChildItem $srcRoot -Recurse -File)) {
    $relative = $file.FullName.Substring($srcRoot.Length + 1)
    $target = Join-Path $baseDir $relative
    $targetDir = Split-Path -Parent $target
    if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
    try {
        Copy-Item $file.FullName $target -Force
    } catch {
        $failed += ($relative + " : " + $_.Exception.Message)
    }
}

if ($failed.Count -gt 0) {
    Write-Host "Erreur : $($failed.Count) fichier(s) n'ont pas pu etre remplaces." -ForegroundColor Red
    foreach ($entry in $failed) { Write-Host "  - $entry" }
    Write-Host "Installation incomplete. Fermez les applications qui utilisent ces fichiers,"
    Write-Host "puis relancez la mise a jour. version.txt n'a pas ete modifie."
    exit 1
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
