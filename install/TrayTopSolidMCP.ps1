# TrayTopSolidMCP - icone systray qui gere le bridge TopSolid MCP
# Style TeamViewer / TopSolid'PDM :
#   - badge d'etat sur l'icone : vert = connecte, orange = connexion en cours, rouge = deconnecte
#   - menu contextuel : etats detailles en tete + actions + parametres
#   - langue = langue de l'OS (fr si OS francais, sinon anglais)
# Aucune ligne de commande : tout se passe dans le systray.

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic

$installDir = Split-Path -Parent $PSScriptRoot
$bridgeDir = Join-Path $installDir 'bridge'

# --- Langue = langue de l'OS ---
if ((Get-UICulture).Name -like 'fr*') { $L = 'fr' } else { $L = 'en' }

$T = @{
    fr = @{
        Conn='Connecte'; ConnBridge='Connecte (port 8080)'; ConnTop='Connecte (port 8090)'
        Connecting='Connexion en cours...'; Detecting='Detection en cours...'
        TopNotFound='Non detecte - lancez TopSolid'; Disconnected='DECONNECTE'
        Bridge='Bridge HTTP'; TopSolid='TopSolid'; Version='Version'
        UrlLabel='URL : http://127.0.0.1:8080/mcp  (cliquer = copier)'
        UrlCopied='URL copiee dans le presse-papiers.'
        Status='Statut'; Reconnect='Reconnecter TopSolid'; Restarted='Bridge redemarre.'
        CheckUpdate='Verifier les mises a jour...'
        Checking='Recherche de mise a jour (v{0})...'
        UpToDate='Vous etes deja a jour (v{0}). Bridge relance.'
        Updated='Mise a jour OK : v{0} -> v{1}. Bridge relance.'
        UpdateFail='Echec de la mise a jour. Bridge relance en v{0}.'
        NoUpdate='update.ps1 introuvable - mise a jour impossible.'
        StopSrv='Arreter le serveur'; Stopped='Bridge arrete.'
        Settings='Parametres'; ApiKey='Cle API...'
        ApiKeyTitle='TopSolid MCP - Cle API'
        ApiKeySaved='Cle API enregistree - bridge relance.'
        ApiKeyGen='Regenerer la cle API'
        ApiKeyGenTitle='TopSolid MCP - Regenerer la cle API'
        ApiKeyGenMsg='L ANCIENNE cle sera revoquee immediatement. Continuer ?'
        ApiKeyGenDone='Nouvelle cle generee et copiee dans le presse-papiers - bridge relance.'
        AutoStart='Demarrage automatique (ouverture de session)'
        AutoOn='Demarrage automatique active.'
        AutoOff='Demarrage automatique desactive.'
        OpenDir="Ouvrir le dossier d'installation"
        Lang="Langue : {0} (langue de l'OS)"
        AutoRelaunch='Bridge inactif - relance automatiquement.'
        TrayConn='TopSolid MCP v{0} - Connecte'
        TrayConnIng='TopSolid MCP v{0} - Connexion en cours'
        TrayDisc='TopSolid MCP v{0} - Deconnecte'
        Started='Bridge demarre (v{0}) - surveillance active.'
        GitHub='GitHub'; Docs='Documentation'; Quit='Quitter'
    }
    en = @{
        Conn='Connected'; ConnBridge='Connected (port 8080)'; ConnTop='Connected (port 8090)'
        Connecting='Connecting...'; Detecting='Detecting...'
        TopNotFound='Not detected - start TopSolid'; Disconnected='DISCONNECTED'
        Bridge='HTTP bridge'; TopSolid='TopSolid'; Version='Version'
        UrlLabel='URL: http://127.0.0.1:8080/mcp  (click = copy)'
        UrlCopied='URL copied to clipboard.'
        Status='Status'; Reconnect='Reconnect to TopSolid'; Restarted='Bridge restarted.'
        CheckUpdate='Check for updates...'
        Checking='Checking for update (v{0})...'
        UpToDate='Already up to date (v{0}). Bridge restarted.'
        Updated='Update OK: v{0} -> v{1}. Bridge restarted.'
        UpdateFail='Update failed. Bridge restarted on v{0}.'
        NoUpdate='update.ps1 not found - update impossible.'
        StopSrv='Stop server'; Stopped='Bridge stopped.'
        Settings='Settings'; ApiKey='API key...'
        ApiKeyTitle='TopSolid MCP - API key'
        ApiKeySaved='API key saved - bridge restarted.'
        ApiKeyGen='Regenerate API key'
        ApiKeyGenTitle='TopSolid MCP - Regenerate API key'
        ApiKeyGenMsg='The OLD key will be revoked immediately. Continue?'
        ApiKeyGenDone='New key generated and copied to clipboard - bridge restarted.'
        AutoStart='Start automatically (logon)'
        AutoOn='Autostart enabled.'; AutoOff='Autostart disabled.'
        OpenDir='Open installation folder'
        Lang='Language: {0} (OS language)'
        AutoRelaunch='Bridge down - relaunched automatically.'
        TrayConn='TopSolid MCP v{0} - Connected'
        TrayConnIng='TopSolid MCP v{0} - Connecting'
        TrayDisc='TopSolid MCP v{0} - Disconnected'
        Started='Bridge started (v{0}) - watchdog active.'
        GitHub='GitHub'; Docs='Documentation'; Quit='Quit'
    }
}
$S = $T[$L]

$script:BridgeProc = $null
$script:ApiKey = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY', 'User')

# Genere une cle API aleatoire de 48 hex chars (24 octets cryptographiques).
# Utilise RNGCryptoServiceProvider : les numeros aleatoires type Get-Random
# ou [Random] ne conviennent pas pour un secret.
function New-ApiKey {
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $bytes = New-Object byte[] 24
    $rng.GetBytes($bytes)
    return ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
}

function Set-ApiKey([string]$k) {
    $script:ApiKey = $k
    [Environment]::SetEnvironmentVariable('TOPSOLID_MCP_API_KEY', $k, 'User')
    Start-Bridge | Out-Null
}
$script:Updating = $false
$script:BridgeStartedAt = [DateTime]::MinValue
$script:State = 'red'

function Get-PortListening([int]$port) {
    try {
        return [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    } catch { return $false }
}

# vert : bridge 8080 OK + TopSolid 8090 detecte
# orange : bridge OK mais TopSolid absent, ou demarrage < 20 s
# rouge : bridge pas a l'ecoute
function Get-BridgeState {
    $p8080 = Get-PortListening 8080
    if (-not $p8080) {
        if (((Get-Date) - $script:BridgeStartedAt).TotalSeconds -lt 20 -and $script:BridgeProc) { return 'orange' }
        return 'red'
    }
    if (-not (Get-PortListening 8090)) { return 'orange' }
    return 'green'
}

function Get-InstalledVersion {
    $v = Join-Path $installDir 'version.txt'
    if (Test-Path $v) { return (Get-Content $v -Raw).Trim() }
    return '?'
}

function Stop-Bridge {
    if ($script:BridgeProc -and -not $script:BridgeProc.HasExited) {
        try { $script:BridgeProc | Stop-Process -Force -ErrorAction SilentlyContinue } catch {}
    }
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
        $script:BridgeStartedAt = Get-Date
        return $true
    } catch { return $false }
}

function Get-UpdateScriptPath {
    $p = Join-Path $installDir 'update.ps1'
    if (Test-Path $p) { return $p }
    $p2 = Join-Path (Join-Path $installDir 'server') 'scripts\update.ps1'
    if (Test-Path $p2) { return $p2 }
    return $null
}

function Invoke-TrayUpdate {
    $upd = Get-UpdateScriptPath
    if (-not $upd) { return @(2, '?', '?') }
    $oldV = Get-InstalledVersion
    $proc = Start-Process -FilePath 'powershell.exe' `
        -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $upd) `
        -WindowStyle Hidden -PassThru -Wait
    $newV = Get-InstalledVersion
    return @($proc.ExitCode, $oldV, $newV)
}

$stateColors = @{
    green  = [System.Drawing.Color]::FromArgb(255, 46, 204, 64)
    orange = [System.Drawing.Color]::FromArgb(255, 255, 133, 27)
    red    = [System.Drawing.Color]::FromArgb(255, 255, 65, 54)
}

function New-StateIcon([string]$state) {
    $bmp = New-Object System.Drawing.Bitmap 32, 32
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 45, 55, 70))
    $g.FillEllipse($body, 1, 1, 26, 26)
    $font = New-Object System.Drawing.Font 'Segoe UI', 11, ([System.Drawing.FontStyle]::Bold)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString('T', $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 2, 27, 26), $fmt)
    $badge = New-Object System.Drawing.SolidBrush $stateColors[$state]
    $g.FillEllipse($badge, 21, 21, 10, 10)
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 1.5
    $g.DrawEllipse($ring, 21, 21, 10, 10)
    $g.Dispose()
    $hicon = $bmp.GetHicon()
    $ico = [System.Drawing.Icon]::FromHandle($hicon)
    $bmp.Dispose()
    return $ico
}

function Update-TrayState {
    $bridgeState = Get-BridgeState
    if ($bridgeState -ne $script:State) {
        $script:State = $bridgeState
        $icon.Icon = New-StateIcon $bridgeState
    }
    $v = Get-InstalledVersion
    switch ($bridgeState) {
        'green' {
            $mEtatBridge.Text = "$($S.Bridge) : $($S.ConnBridge)"
            $mEtatTop.Text    = "$($S.TopSolid) : $($S.ConnTop)"
            $icon.Text = $S.TrayConn -f $v
        }
        'orange' {
            if ((Get-PortListening 8080) -and -not (Get-PortListening 8090)) {
                $mEtatBridge.Text = "$($S.Bridge) : $($S.ConnBridge)"
                $mEtatTop.Text    = "$($S.TopSolid) : $($S.TopNotFound)"
            } else {
                $mEtatBridge.Text = "$($S.Bridge) : $($S.Connecting)"
                $mEtatTop.Text    = "$($S.TopSolid) : $($S.Detecting)"
            }
            $icon.Text = $S.TrayConnIng -f $v
        }
        default {
            $mEtatBridge.Text = "$($S.Bridge) : $($S.Disconnected)"
            $mEtatTop.Text    = "$($S.TopSolid) : ---"
            $icon.Text = $S.TrayDisc -f $v
        }
    }
    $mEtatVersion.Text = "$($S.Version) : v$v"
}$icon = New-Object System.Windows.Forms.NotifyIcon
$icon.Icon = New-StateIcon 'red'
$icon.Text = 'TopSolid MCP'
$icon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip

$mEtatBridge = New-Object System.Windows.Forms.ToolStripMenuItem "$($S.Bridge) : ---"
$mEtatBridge.Enabled = $false
$menu.Items.Add($mEtatBridge) | Out-Null
$mEtatTop = New-Object System.Windows.Forms.ToolStripMenuItem "$($S.TopSolid) : ---"
$mEtatTop.Enabled = $false
$menu.Items.Add($mEtatTop) | Out-Null
$mEtatVersion = New-Object System.Windows.Forms.ToolStripMenuItem "$($S.Version) : ---"
$mEtatVersion.Enabled = $false
$menu.Items.Add($mEtatVersion) | Out-Null
$mEtatLang = New-Object System.Windows.Forms.ToolStripMenuItem ($S.Lang -f $L.ToUpper())
$mEtatLang.Enabled = $false
$menu.Items.Add($mEtatLang) | Out-Null
$mEtatUrl = New-Object System.Windows.Forms.ToolStripMenuItem $S.UrlLabel
$mEtatUrl.Add_Click({
    [System.Windows.Forms.Clipboard]::SetText('http://127.0.0.1:8080/mcp')
    $icon.ShowBalloonTip(2000, 'TopSolid MCP', $S.UrlCopied, [System.Windows.Forms.ToolTipIcon]::Info)
})
$menu.Items.Add($mEtatUrl) | Out-Null
$menu.Items.Add('-') | Out-Null

$mStatut = New-Object System.Windows.Forms.ToolStripMenuItem $S.Status
$mStatut.Add_Click({
    if (-not (Get-PortListening 8080)) { Start-Bridge | Out-Null }
    Update-TrayState
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', $icon.Text, [System.Windows.Forms.ToolTipIcon]::Info)
})
$menu.Items.Add($mStatut) | Out-Null

$mReconnect = New-Object System.Windows.Forms.ToolStripMenuItem $S.Reconnect
$mReconnect.Add_Click({
    Stop-Bridge; Start-Sleep 1; Start-Bridge | Out-Null
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.Restarted, [System.Windows.Forms.ToolTipIcon]::Info)
})
$menu.Items.Add($mReconnect) | Out-Null

$mUpdate = New-Object System.Windows.Forms.ToolStripMenuItem $S.CheckUpdate
$mUpdate.Add_Click({
    if ($script:Updating) { return }
    $script:Updating = $true
    $mUpdate.Enabled = $false
    $vAvant = Get-InstalledVersion
    $icon.ShowBalloonTip(4000, 'TopSolid MCP', ($S.Checking -f $vAvant), [System.Windows.Forms.ToolTipIcon]::Info)
    Stop-Bridge
    $res = Invoke-TrayUpdate
    $code = $res[0]; $oldV = $res[1]; $newV = $res[2]
    Start-Bridge | Out-Null
    $script:Updating = $false
    $mUpdate.Enabled = $true
    switch ($code) {
        0 {
            if ($newV -ne $oldV -and $newV -ne '?') {
                $icon.ShowBalloonTip(5000, 'TopSolid MCP', ($S.Updated -f $oldV, $newV), [System.Windows.Forms.ToolTipIcon]::Info)
            } else {
                $icon.ShowBalloonTip(4000, 'TopSolid MCP', ($S.UpToDate -f $oldV), [System.Windows.Forms.ToolTipIcon]::Info)
            }
        }
        1 { $icon.ShowBalloonTip(5000, 'TopSolid MCP', ($S.UpdateFail -f (Get-InstalledVersion)), [System.Windows.Forms.ToolTipIcon]::Error) }
        default { $icon.ShowBalloonTip(4000, 'TopSolid MCP', $S.NoUpdate, [System.Windows.Forms.ToolTipIcon]::Error) }
    }
})
$menu.Items.Add($mUpdate) | Out-Null

$menu.Items.Add('-') | Out-Null

$mGithub = New-Object System.Windows.Forms.ToolStripMenuItem $S.GitHub
$mGithub.Add_Click({ Start-Process 'https://github.com/Julien38300/topsolid-automation-mcp' })
$menu.Items.Add($mGithub) | Out-Null
$mDocs = New-Object System.Windows.Forms.ToolStripMenuItem $S.Docs
$mDocs.Add_Click({ Start-Process 'https://github.com/Julien38300/topsolid-automation-mcp/tree/main/docs' })
$menu.Items.Add($mDocs) | Out-Null

$menu.Items.Add('-') | Out-Null

$mStop = New-Object System.Windows.Forms.ToolStripMenuItem $S.StopSrv
$mStop.Add_Click({
    Stop-Bridge
    $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.Stopped, [System.Windows.Forms.ToolTipIcon]::Warning)
})
$menu.Items.Add($mStop) | Out-Null

$mSettings = New-Object System.Windows.Forms.ToolStripMenuItem $S.Settings
$menu.Items.Add($mSettings) | Out-Null

$mApiKey = New-Object System.Windows.Forms.ToolStripMenuItem $S.ApiKey
$mApiKey.Add_Click({
    # Boite pre-remplie avec la cle actuelle ; VIDE = generer une nouvelle cle
    # (plus besoin de l inventer), Annuler = ne rien changer.
    $k = [Microsoft.VisualBasic.Interaction]::InputBox('TOPSOLID_MCP_API_KEY (laisser VIDE pour en generer une) :', $S.ApiKeyTitle, $script:ApiKey)
    if ($k -eq '') {
        $k = New-ApiKey
    }
    if ($k) {
        Set-ApiKey $k
        $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.ApiKeySaved, [System.Windows.Forms.ToolTipIcon]::Info)
    }
})
$mSettings.DropDownItems.Add($mApiKey) | Out-Null

$mApiKeyGen = New-Object System.Windows.Forms.ToolStripMenuItem $S.ApiKeyGen
$mApiKeyGen.Add_Click({
    $r = [System.Windows.Forms.MessageBox]::Show($S.ApiKeyGenMsg, $S.ApiKeyGenTitle, [System.Windows.Forms.MessageBoxButtons]::YesNo, [System.Windows.Forms.MessageBoxIcon]::Warning)
    if ($r -eq [System.Windows.Forms.DialogResult]::Yes) {
        $k = New-ApiKey
        Set-ApiKey $k
        Set-Clipboard -Value $k
        $icon.ShowBalloonTip(5000, 'TopSolid MCP', $S.ApiKeyGenDone, [System.Windows.Forms.ToolTipIcon]::Info)
    }
})
$mSettings.DropDownItems.Add($mApiKeyGen) | Out-Null

$lnkPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'TopSolidMCP Tray.lnk'
$mAutoStart = New-Object System.Windows.Forms.ToolStripMenuItem $S.AutoStart
$mAutoStart.Checked = (Test-Path $lnkPath)
$mAutoStart.Add_Click({
    if ($mAutoStart.Checked) {
        Remove-Item $lnkPath -Force -ErrorAction SilentlyContinue
        $mAutoStart.Checked = $false
        $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.AutoOff, [System.Windows.Forms.ToolTipIcon]::Info)
    } else {
        $w = New-Object -ComObject WScript.Shell
        $sc = $w.CreateShortcut($lnkPath)
        $sc.TargetPath = 'powershell.exe'
        $sc.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSScriptRoot\TrayTopSolidMCP.ps1`""
        $sc.Save()
        $mAutoStart.Checked = $true
        $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.AutoOn, [System.Windows.Forms.ToolTipIcon]::Info)
    }
})
$mSettings.DropDownItems.Add($mAutoStart) | Out-Null

$mOpenDir = New-Object System.Windows.Forms.ToolStripMenuItem $S.OpenDir
$mOpenDir.Add_Click({ Start-Process explorer.exe $installDir })
$mSettings.DropDownItems.Add($mOpenDir) | Out-Null

$menu.Items.Add('-') | Out-Null

$mQuit = New-Object System.Windows.Forms.ToolStripMenuItem $S.Quit
$mQuit.Add_Click({
    Stop-Bridge
    $icon.Visible = $false
    [System.Windows.Forms.Application]::Exit()
})
$menu.Items.Add($mQuit) | Out-Null

$icon.ContextMenuStrip = $menu

$timerState = New-Object System.Windows.Forms.Timer
$timerState.Interval = 2000
$timerState.Add_Tick({ try { Update-TrayState } catch {} })
$timerState.Start()

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 30000
$timer.Add_Tick({
    try {
        if (-not $script:Updating -and -not (Get-PortListening 8080) -and $script:State -ne 'orange') {
            Start-Bridge | Out-Null
            $icon.ShowBalloonTip(3000, 'TopSolid MCP', $S.AutoRelaunch, [System.Windows.Forms.ToolTipIcon]::Info)
        }
    } catch {}
})
$timer.Start()

if (-not (Get-PortListening 8080)) { Start-Bridge | Out-Null }
Update-TrayState
$icon.ShowBalloonTip(4000, 'TopSolid MCP', $icon.Text, [System.Windows.Forms.ToolTipIcon]::Info)

[System.Windows.Forms.Application]::Run()