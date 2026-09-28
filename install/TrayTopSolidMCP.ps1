# TrayTopSolidMCP - icone systray qui gere le bridge TopSolid MCP
# Demarre le bridge a l'ouverture de session, le surveille, le relance s'il meurt,
# et applique les mises a jour (update.ps1) sans aucune ligne de commande.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Ce script vit dans install/ ; le bridge est ../bridge
$installDir = Split-Path -Parent $PSScriptRoot      # racine de l'installation
$bridgeDir = Join-Path $installDir 'bridge'
if (-not (Test-Path (Join-Path $bridgeDir 'start-bridge.ps1'))) {
    $bridgeDir = Join-Path $installDir 'bridge'
}

$script:BridgeProc = $null
$script:ApiKey = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY', 'User')
$script:Updating = $false

function Get-PortListening {
    try {
        return [bool](Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue)
    } catch { return $false }
}

# version.txt est a la racine de l'installation (layout release)
function Get-InstalledVersion {
    $v = Join-Path $installDir 'version.txt'
    if (Test-Path $v) { return (Get-Content $v -Raw).Trim() }
    return '?'
}

function Stop-Bridge {
    if ($script:BridgeProc -and -not $script:BridgeProc.HasExited) {
        try { $script:BridgeProc | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
    }
    # Tue aussi les enfants node/mcp-proxy du bridge
    Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match 'mcp-proxy' } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $script:BridgeProc = $null
}

function Start-Bridge {
    Stop-Bridge
    if (-not $script:ApiKey) {
        $script:ApiKey = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY', 'User')
    }
    $env:TOPSOLID_MCP_API_KEY = $script:ApiKey
    try {
        $script:BridgeProc = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $bridgeDir 'start-bridge.ps1'), '-Open') `
            -WindowStyle Hidden -PassThru
        return $true
    } catch { return $false }
}

# update.ps1 : layout release = a la racine, depot = server/scripts/update.ps1
function Get-UpdateScriptPath {
    $p = Join-Path $installDir 'update.ps1'
    if (Test-Path $p) { return $p }
    $p2 = Join-Path (Join-Path $installDir 'server') 'scripts\update.ps1'
    if (Test-Path $p2) { return $p2 }
    return $null
}

function Invoke-TrayUpdate {
    # Appelle update.ps1 (cache, avec attente). Retourne [exitCode, oldV, newV]
    $upd = Get-UpdateScriptPath
    if (-not $upd) { return @(2, '?', '?') }
    $oldV = Get-InstalledVersion
    $proc = Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $upd) `
        -WindowStyle Hidden -PassThru -Wait
    $newV = Get-InstalledVersion
    return @($proc.ExitCode, $oldV, $newV)
}

# --- Icone systray ---
$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = [System.Drawing.SystemIcons]::Application
$icon.Text = 'TopSolid MCP'
$icon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip

$mStatut = New-Object System.Windows.Forms.ToolStripMenuItem 'Statut'
$mStatut.Add_Click({
    $ok = Get-PortListening
    $msg = if ($ok) { "Bridge actif - port 8080 OK (v$(Get-InstalledVersion))" } else { 'Bridge ARRETE - redemarrage...' }
    if (-not $ok) { Start-Bridge | Out-Null }
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', $msg, [System.Windows.Forms.ToolTipIcon]::Info)
})
$menu.Items.Add($mStatut) | Out-Null

$mRestart = New-Object System.Windows.Forms.ToolStripMenuItem 'Redemarrer'
$mRestart.Add_Click({
    Stop-Bridge; Start-Sleep 1; Start-Bridge | Out-Null
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', 'Bridge redemarre.', [System.Windows.Forms.ToolTipIcon]::Info)
})
$menu.Items.Add($mRestart) | Out-Null

# --- Mise a jour integree au tray ---
$mUpdate = New-Object System.Windows.Forms.ToolStripMenuItem 'Mettre a jour'
$mUpdate.Add_Click({
    if ($script:Updating) { return }
    $script:Updating = $true
    $mUpdate.Enabled = $false
    $vAvant = Get-InstalledVersion
    $icon.ShowBalloonTip(4000, 'TopSolid MCP', "Recherche de mise a jour (v$vAvant)...", [System.Windows.Forms.ToolTipIcon]::Info)

    # Le chien de garde ne doit pas relancer le bridge pendant l'update
    Stop-Bridge
    $res = Invoke-TrayUpdate
    $code = $res[0]; $oldV = $res[1]; $newV = $res[2]

    Start-Bridge | Out-Null
    $script:Updating = $false
    $mUpdate.Enabled = $true

    switch ($code) {
        0 {
            if ($newV -ne $oldV -and $newV -ne '?') {
                $icon.ShowBalloonTip(5000, 'TopSolid MCP', "Mise a jour OK : v$oldV -> v$newV. Bridge relance.", [System.Windows.Forms.ToolTipIcon]::Info)
            } else {
                $icon.ShowBalloonTip(4000, 'TopSolid MCP', "Vous etes deja a jour (v$oldV). Bridge relance.", [System.Windows.Forms.ToolTipIcon]::Info)
            }
        }
        1 { $icon.ShowBalloonTip(5000, 'TopSolid MCP', "Echec de la mise a jour (voir message). Bridge relance en v$(Get-InstalledVersion).", [System.Windows.Forms.ToolTipIcon]::Error) }
        default { $icon.ShowBalloonTip(4000, 'TopSolid MCP', 'update.ps1 introuvable - mise a jour impossible.', [System.Windows.Forms.ToolTipIcon]::Error) }
    }
})
$menu.Items.Add($mUpdate) | Out-Null

$mStop = New-Object System.Windows.Forms.ToolStripMenuItem 'Arreter'
$mStop.Add_Click({
    Stop-Bridge
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', 'Bridge arrete.', [System.Windows.Forms.ToolTipIcon]::Warning)
})
$menu.Items.Add($mStop) | Out-Null

$menu.Items.Add('-') | Out-Null

$mQuit = New-Object System.Windows.Forms.ToolStripMenuItem 'Quitter'
$mQuit.Add_Click({
    Stop-Bridge
    $icon.Visible = $false
    [System.Windows.Forms.Application]::Exit()
})
$menu.Items.Add($mQuit) | Out-Null

$icon.ContextMenuStrip = $menu

# --- Chien de garde : toutes les 30 s, si le port ne repond pas, on relance ---
# (suspendu pendant une mise a jour pour ne pas la saboter)
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 30000
$timer.Add_Tick({
    try {
        if (-not $script:Updating -and -not (Get-PortListening)) {
            Start-Bridge | Out-Null
            $icon.ShowBalloonTip(3000, 'TopSolid MCP', 'Bridge inactif - relance automatiquement.', [System.Windows.Forms.ToolTipIcon]::Info)
        }
    } catch {}
})
$timer.Start()

# --- Demarrage initial ---
if (-not (Get-PortListening)) { Start-Bridge | Out-Null }
$icon.ShowBalloonTip(4000, 'TopSolid MCP', "Bridge demarre (v$(Get-InstalledVersion)) - surveillance active.", [System.Windows.Forms.ToolTipIcon]::Info)

[System.Windows.Forms.Application]::Run()