@echo off
chcp 65001 >nul
title TopSolid MCP - Installation automatique
cd /d "%~dp0"

echo ==========================================================
echo   TopSolid MCP - Installation en un double-clic
echo ==========================================================
echo.
echo Ce programme :
echo   1. Nettoie une eventuelle ancienne installation v1.7
echo      (taches planifiees Node/bridge, raccourci tray PS1)
echo   2. Genere une cle API chiffree (DPAPI, presse-papiers)
echo   3. Cree la tache planifiee du serveur (HTTP natif, port 8080)
echo   4. Demarre le serveur (icone systray incluse)
echo.
echo Aucune ligne de commande a taper ensuite : tout se gere
echo depuis l icone dans le systray (pres de l horloge).
echo.
pause

REM ================= 1. Nettoyage installation v1.7 =================
echo [1/4] Nettoyage des anciens composants (v1.7 Node/bridge)...
REM La tache SYSTEM TopSolidMcpBridge (setup manuel), la chaine node de
REM l installer v1.7 (tache TopSolidMcpTray + raccourci Startup) sont
REM remplaces en v1.8.0 par une tache planifiee unique qui lance
REM directement l executable. Le serveur natif degrade en stdio-only si
REM un process legacy garde le port 8080 : on retire tout AVANT v1.8.
schtasks /Query /TN "TopSolidMcpBridge" >nul 2>&1
if not errorlevel 1 (
    echo   - suppression de la tache planifiee TopSolidMcpBridge...
    schtasks /End /TN "TopSolidMcpBridge" >nul 2>&1
    schtasks /Delete /TN "TopSolidMcpBridge" /F >nul 2>&1
)
schtasks /Query /TN "TopSolidMcpTray" >nul 2>&1
if not errorlevel 1 (
    echo   - suppression de la tache planifiee TopSolidMcpTray ^(tray v1.7^)...
    schtasks /End /TN "TopSolidMcpTray" >nul 2>&1
    schtasks /Delete /TN "TopSolidMcpTray" /F >nul 2>&1
)
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TopSolidMCP Tray.lnk" (
    echo   - suppression du raccourci tray v1.7 du dossier Startup...
    del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TopSolidMCP Tray.lnk" >nul 2>&1
)
REM Les process legacy vises precisement (n tue pas node/powershell en bloc) :
REM node mcp-proxy (bridge v1.7), watcher PS1 TrayTopSolidMCP (relanait l ancien)
REM et toute instance exe residuelle (le mutex Global bloque le demarrage v1.8).
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='node.exe' OR Name='powershell.exe'\" | Where-Object { $_.CommandLine -match 'mcp-proxy|TrayTopSolidMCP|start-bridge' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }"
taskkill /IM TopSolidMcpServer.exe /F >nul 2>&1
echo   Nettoyage OK.
echo.

REM ================= 2. Cle API =================
echo [2/4] Generation de la cle API...
REM Si aucune cle n existe deja : generation aleatoire, copie dans le
REM presse-papiers et affichage. Le serveur la lit ensuite dans
REM settings.json chiffre DPAPI ; env User TOPSOLID_MCP_API_KEY reste
REM acceptee comme source de migration.
powershell -NoProfile -Command "$k = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY','User'); if (-not $k) { $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create(); $b = New-Object byte[] 24; $rng.GetBytes($b); $k = ($b | ForEach-Object { $_.ToString('x2') }) -join ''; [Environment]::SetEnvironmentVariable('TOPSOLID_MCP_API_KEY', $k, 'User'); Set-Clipboard -Value $k; Write-Host ''; Write-Host 'Cle API generee automatiquement :' -ForegroundColor Green; Write-Host $k -ForegroundColor Green; Write-Host 'Elle est aussi dans le presse-papiers.' -ForegroundColor Yellow } else { Write-Host 'Cle API existante conservee (env TOPSOLID_MCP_API_KEY).' }"
echo.

REM ================= 3. Tache planifiee serveur =================
echo [3/4] Creation de la tache planifiee TopSolidMcpServer...
REM Lance TopSolidMcpServer.exe --http-standalone (HTTP natif + keepalive
REM apres stdin EOF, pas de serveur stdio) au login, session interactive,
REM privileges eleves. v1.8.1 : l exe est une application CONSOLE — lancé
REM directement par une tache ONLOGON interactive, il OUVRAIT une fenetre
REM cmd qui restait ouverte a chaque session. conhost --headless execute le
REM process avec une console allouee mais INVISIBLE : stderr reste redirige
REM vers server.log (v1.8.1), aucune fenetre, aucun changement de comportement.
schtasks /Create /F /TN "TopSolidMcpServer" /SC ONLOGON /RL HIGHEST /IT /TR "conhost.exe --headless \"%~dp0..\TopSolidMcpServer.exe\" --http-standalone" >nul 2>&1
if errorlevel 1 (
    echo   AVERTISSEMENT: tache planifiee non creee ^(droits admin requis^).
    echo   Le serveur peut etre demarre par double-clic sur TopSolidMcpServer.exe.
)
echo.

REM ================= 3bis. Demarrage =================
echo [3bis/4] Demarrage du serveur...
REM v1.8.0 : le tray fait partie de l executable. Une SEULE instance s execute :
REM tache planifiee si elle a ete creee, sinon demarrage direct double-clic
REM (le mutex singleton garantit qu un seul serveur ecoute le port 8080).
REM v1.8.1 : demarrage direct = console MINIMISEE (pas de fenetre plein ecran).
schtasks /Query /TN "TopSolidMcpServer" >nul 2>&1
if not errorlevel 1 (
    schtasks /Run /TN "TopSolidMcpServer" >nul 2>&1
) else (
    start /min "" "%~dp0..\TopSolidMcpServer.exe"
)
echo.

echo ==========================================================
echo   Installation terminee !
echo   - Serveur MCP natif sur le port 8080 (auth X-API-Key)
echo   - Cle API chiffree DPAPI dans settings.json
echo   - Au prochain demarrage du PC, tout repartira seul
echo ==========================================================
echo.
echo Cette fenetre se ferme dans 10 secondes...
timeout /t 10 >nul
