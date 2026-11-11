@echo off
chcp 65001 >nul
title TopSolid MCP - Installation / Installer
cd /d "%~dp0"

REM ================= v1.8.19 (issue #21): bilingual installer =================
REM French messages on French Windows display languages, English everywhere
REM else. The display language is used - never the regional date format.
set "MCP_LANG=en"
for /f %%i in ('powershell -NoProfile -Command "if ([CultureInfo]::CurrentUICulture.Name -like 'fr*') {'fr'} else {'en'}"') do set "MCP_LANG=%%i"
if not "%MCP_LANG%"=="fr" if not "%MCP_LANG%"=="en" set "MCP_LANG=en"

if "%MCP_LANG%"=="fr" goto lang_fr

:lang_en
set "MSG_BANNER=TopSolid MCP - One double-click installation"
set "MSG_INTRO=This program:"
set "MSG_STEP1A=  1. Cleans up any previous v1.7 installation"
set "MSG_STEP1B=     scheduled Node/bridge tasks, PS1 tray shortcut"
set "MSG_STEP2=  2. Generates an encrypted API key - DPAPI, clipboard"
set "MSG_STEP3=  3. Creates the server scheduled task - native HTTP, port 8080"
set "MSG_STEP4=  4. Starts the server - system tray icon included"
set "MSG_NOCLI1=No command line to type afterwards: everything is managed"
set "MSG_NOCLI2=from the tray icon next to the clock."
set "MSG_CLEAN1=[1/4] Cleaning up the old v1.7 components - Node/bridge..."
set "MSG_DELBRIDGE=- removing the TopSolidMcpBridge scheduled task..."
set "MSG_DELTRAY=- removing the TopSolidMcpTray scheduled task - v1.7 tray..."
set "MSG_DELLNK=- removing the v1.7 tray shortcut from the Startup folder..."
set "MSG_CLEANOK=Cleanup OK."
set "MSG_KEYGEN=[2/4] Generating the API key..."
set "MSG_TASK=[3/4] Creating the TopSolidMcpServer scheduled task..."
set "MSG_WARN1=WARNING: scheduled task not created - admin rights required."
set "MSG_WARN2=The server can still be started by double-clicking TopSolidMcpServer.exe."
set "MSG_START=[3bis/4] Starting the server..."
set "MSG_DONE=Installation complete!"
set "MSG_DONE1=- Native MCP server on port 8080 - X-API-Key auth"
set "MSG_DONE2=- DPAPI-encrypted API key in settings.json"
set "MSG_DONE3=- On the next PC startup, everything restarts on its own"
set "MSG_BYE=This window closes in 10 seconds..."
goto lang_done

:lang_fr
set "MSG_BANNER=TopSolid MCP - Installation en un double-clic"
set "MSG_INTRO=Ce programme :"
set "MSG_STEP1A=  1. Nettoie une eventuelle ancienne installation v1.7"
set "MSG_STEP1B=     taches planifiees Node/bridge, raccourci tray PS1"
set "MSG_STEP2=  2. Genere une cle API chiffree - DPAPI, presse-papiers"
set "MSG_STEP3=  3. Cree la tache planifiee du serveur - HTTP natif, port 8080"
set "MSG_STEP4=  4. Demarre le serveur - icone systray incluse"
set "MSG_NOCLI1=Aucune ligne de commande a taper ensuite : tout se gere"
set "MSG_NOCLI2=depuis l icone dans le systray - pres de l horloge."
set "MSG_CLEAN1=[1/4] Nettoyage des anciens composants v1.7 Node/bridge..."
set "MSG_DELBRIDGE=- suppression de la tache planifiee TopSolidMcpBridge..."
set "MSG_DELTRAY=- suppression de la tache planifiee TopSolidMcpTray - tray v1.7..."
set "MSG_DELLNK=- suppression du raccourci tray v1.7 du dossier Startup..."
set "MSG_CLEANOK=Nettoyage OK."
set "MSG_KEYGEN=[2/4] Generation de la cle API..."
set "MSG_TASK=[3/4] Creation de la tache planifiee TopSolidMcpServer..."
set "MSG_WARN1=AVERTISSEMENT: tache planifiee non creee - droits admin requis."
set "MSG_WARN2=Le serveur peut etre demarre par double-clic sur TopSolidMcpServer.exe."
set "MSG_START=[3bis/4] Demarrage du serveur..."
set "MSG_DONE=Installation terminee !"
set "MSG_DONE1=- Serveur MCP natif sur le port 8080 - auth X-API-Key"
set "MSG_DONE2=- Cle API chiffree DPAPI dans settings.json"
set "MSG_DONE3=- Au prochain demarrage du PC, tout repartira seul"
set "MSG_BYE=Cette fenetre se ferme dans 10 secondes..."
:lang_done

echo ==========================================================
echo   %MSG_BANNER%
echo ==========================================================
echo.
echo %MSG_INTRO%
echo %MSG_STEP1A%
echo %MSG_STEP1B%
echo %MSG_STEP2%
echo %MSG_STEP3%
echo %MSG_STEP4%
echo.
echo %MSG_NOCLI1%
echo %MSG_NOCLI2%
echo.
pause

REM ================= 1. Nettoyage installation v1.7 =================
echo %MSG_CLEAN1%
REM La tache SYSTEM TopSolidMcpBridge (setup manuel), la chaine node de
REM l installer v1.7 (tache TopSolidMcpTray + raccourci Startup) sont
REM remplaces en v1.8.0 par une tache planifiee unique qui lance
REM directement l executable. Le serveur natif degrade en stdio-only si
REM un process legacy garde le port 8080 : on retire tout AVANT v1.8.
schtasks /Query /TN "TopSolidMcpBridge" >nul 2>&1
if not errorlevel 1 (
    echo   %MSG_DELBRIDGE%
    schtasks /End /TN "TopSolidMcpBridge" >nul 2>&1
    schtasks /Delete /TN "TopSolidMcpBridge" /F >nul 2>&1
)
schtasks /Query /TN "TopSolidMcpTray" >nul 2>&1
if not errorlevel 1 (
    echo   %MSG_DELTRAY%
    schtasks /End /TN "TopSolidMcpTray" >nul 2>&1
    schtasks /Delete /TN "TopSolidMcpTray" /F >nul 2>&1
)
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TopSolidMCP Tray.lnk" (
    echo   %MSG_DELLNK%
    del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TopSolidMCP Tray.lnk" >nul 2>&1
)
REM Les process legacy vises precisement (n tue pas node/powershell en bloc) :
REM node mcp-proxy (bridge v1.7), watcher PS1 TrayTopSolidMCP (relanait l ancien)
REM et toute instance exe residuelle (le mutex Global bloque le demarrage v1.8).
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='node.exe' OR Name='powershell.exe'\" | Where-Object { $_.CommandLine -match 'mcp-proxy|TrayTopSolidMCP|start-bridge' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }"
taskkill /IM TopSolidMcpServer.exe /F >nul 2>&1
echo   %MSG_CLEANOK%
echo.

REM ================= 2. Cle API =================
echo %MSG_KEYGEN%
REM Si aucune cle n existe deja : generation aleatoire, copie dans le
REM presse-papiers et affichage. Le serveur la lit ensuite dans
REM settings.json chiffre DPAPI ; env User TOPSOLID_MCP_API_KEY reste
REM acceptee comme source de migration.
powershell -NoProfile -Command "$fr = [CultureInfo]::CurrentUICulture.Name -like 'fr*'; $k = [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY','User'); if (-not $k) { $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create(); $b = New-Object byte[] 24; $rng.GetBytes($b); $k = ($b | ForEach-Object { $_.ToString('x2') }) -join ''; [Environment]::SetEnvironmentVariable('TOPSOLID_MCP_API_KEY', $k, 'User'); Set-Clipboard -Value $k; Write-Host ''; if ($fr) { Write-Host 'Cle API generee automatiquement :' -ForegroundColor Green; Write-Host $k -ForegroundColor Green; Write-Host 'Elle est aussi dans le presse-papiers.' -ForegroundColor Yellow } else { Write-Host 'API key generated automatically:' -ForegroundColor Green; Write-Host $k -ForegroundColor Green; Write-Host 'It was also copied to the clipboard.' -ForegroundColor Yellow } } else { if ($fr) { Write-Host 'Cle API existante conservee (env TOPSOLID_MCP_API_KEY).' } else { Write-Host 'Existing API key kept (env TOPSOLID_MCP_API_KEY).' } }"
echo.

REM ================= 3. Tache planifiee serveur =================
echo %MSG_TASK%
REM Lance TopSolidMcpServer.exe --http-standalone (HTTP natif + keepalive
REM apres stdin EOF, pas de serveur stdio) au login, session interactive,
REM privileges eleves. v1.8.1 : l exe est une application CONSOLE — lancé
REM directement par une tache ONLOGON interactive, il OUVRAIT une fenetre
REM cmd qui restait ouverte a chaque session. conhost --headless execute le
REM process avec une console allouee mais INVISIBLE : stderr reste redirige
REM vers server.log (v1.8.1), aucune fenetre, aucun changement de comportement.
schtasks /Create /F /TN "TopSolidMcpServer" /SC ONLOGON /RL HIGHEST /IT /TR "conhost.exe --headless \"%~dp0..\TopSolidMcpServer.exe\" --http-standalone" >nul 2>&1
if errorlevel 1 (
    echo   %MSG_WARN1%
    echo   %MSG_WARN2%
)
echo.

REM ================= 3bis. Demarrage =================
echo %MSG_START%
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
echo   %MSG_DONE%
echo   %MSG_DONE1%
echo   %MSG_DONE2%
echo   %MSG_DONE3%
echo ==========================================================
echo.
echo %MSG_BYE%
timeout /t 10 >nul
