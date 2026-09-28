# TrayTopSolidMCP - icone systray qui gere le bridge TopSolid MCP
# Demarre le bridge a l'ouverture de session, le surveille, le relance s'il meurt.
# Aucune ligne de commande necessaire : tout se passe dans le systray.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Ce script vit dans install/ ; le bridge est ../bridge
$bridgeDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'bridge'
if (-not (Test-Path (Join-Path $bridgeDir 'start-bridge.ps1'))) {
    # Layout release : install/ a cote de bridge/
    $alt = Join-Path (Split-Path -Parent $PSScriptRoot) 'bridge'
    if (Test-Path (Join-Path $alt 'start-bridge.ps1')) { $bridgeDir = $alt }
}

$script:BridgeProc = $null
$script:ApiKey = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY', 'User')

function Get-PortListening {
    try {
        return [bool](Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue)
    } catch { return $false }
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

# --- Icone systray ---
$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = [System.Drawing.SystemIcons]::Application
$icon.Text = 'TopSolid MCP'
$icon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip

$mStatut = New-Object System.Windows.Forms.ToolStripMenuItem 'Statut'
$mStatut.Add_Click({
    $ok = Get-PortListening
    $msg = if ($ok) { 'Bridge actif - port 8080 OK' } else { 'Bridge ARRETE - redemarrage...' }
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
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 30000
$timer.Add_Tick({
    try {
        if (-not (Get-PortListening)) {
            Start-Bridge | Out-Null
            $icon.ShowBalloonTip(3000, 'TopSolid MCP', 'Bridge inactif - relance automatiquement.', [System.Windows.Forms.ToolTipIcon]::Info)
        }
    } catch {}
})
$timer.Start()

# --- Demarrage initial ---
if (-not (Get-PortListening)) { Start-Bridge | Out-Null }
$icon.ShowBalloonTip(4000, 'TopSolid MCP', 'Bridge demarre - surveillance active (icone systray).', [System.Windows.Forms.ToolTipIcon]::Info)

[System.Windows.Forms.Application]::Run()