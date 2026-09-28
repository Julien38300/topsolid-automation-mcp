@echo off
chcp 65001 >nul
title TopSolid MCP - Installation automatique
cd /d "%~dp0"

echo ==========================================================
echo   TopSolid MCP - Installation en un double-clic
echo ==========================================================
echo.
echo Ce programme :
echo   1. Verifie Node.js (l'installe via winget si absent)
echo   2. Installe les composants du bridge
echo   3. Demarre le bridge TopSolid MCP sur le port 8080
echo   4. Installe l'icone systray (demarrage auto a l'ouverture
echo      de session, surveillance automatique)
echo.
echo Aucune ligne de commande a taper ensuite : tout se gere
echo depuis l'icone dans le systray (pres de l'horloge).
echo.
pause

REM ================= 1. Node.js =================
where node >nul 2>&1
if errorlevel 1 (
    echo [1/4] Node.js absent - installation via winget...
    winget install OpenJS.NodeJS.LTS --silent --accept-package-agreements --accept-source-agreements
    if errorlevel 1 (
        echo ERREUR: impossible d'installer Node.js automatiquement.
        echo Installe-le depuis https://nodejs.org puis relance ce fichier.
        pause
        exit /b 1
    )
    REM node pas encore dans le PATH de cette session
    set "PATH=%PATH%;%ProgramFiles%\nodejs"
) else (
    echo [1/4] Node.js OK
)
echo.

REM ================= 2. Composants du bridge =================
echo [2/4] Installation des composants du bridge...
cd /d "%~dp0..\bridge"
if not exist "node_modules\mcp-proxy" (
    call npm install
    if errorlevel 1 (
        echo ERREUR: npm install a echoue.
        pause
        exit /b 1
    )
) else (
    echo Composants deja installes.
)
echo.

REM ================= 2bis. Nettoyage ancienne installation =================
REM Les installations anterieures (setup manuel) utilisaient une tache planifiee
REM SYSTEM "TopSolidMcpBridge" lancee au boot avec --no-tray. Elle entre en
REM conflit mutex avec l instance utilisateur : le serveur SYSTEM ou utilisateur
REM detecte "Another instance running" et exit, le bridge ne s ouvre jamais.
REM On la supprime si presente, et on tue toute instance residuelle du serveur.
schtasks /Query /TN "TopSolidMcpBridge" >nul 2>&1
if not errorlevel 1 (
    echo Nettoyage de l ancienne tache planifiee TopSolidMcpBridge SYSTEM...
    schtasks /End /TN "TopSolidMcpBridge" >nul 2>&1
    schtasks /Delete /TN "TopSolidMcpBridge" /F >nul 2>&1
    taskkill /IM TopSolidMcpServer.exe /F >nul 2>&1
    echo Ancienne tache supprimee.
)
echo.

REM ================= 3. Cle API =================
echo [3/4] Configuration de la cle API...
REM Si aucune cle n existe deja : on en genere une aleatoire (48 hex),
REM on la copie dans le presse-papiers et on l affiche a l ecran.
REM L utilisateur n a PLUS besoin d inventer une cle.
powershell -NoProfile -Command "if (-not [Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY','User')) { $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider; $b = New-Object byte[] 24; $rng.GetBytes($b); $k = ($b | ForEach-Object { $_.ToString('x2') }) -join ''; [Environment]::SetEnvironmentVariable('TOPSOLID_MCP_API_KEY', $k, 'User'); Set-Clipboard -Value $k; Write-Host ''; Write-Host 'Cle API generee automatiquement :' -ForegroundColor Green; Write-Host $k -ForegroundColor Green; Write-Host 'Elle est aussi dans le presse-papiers - tu peux la donner a Noemid-H / ton admin.' -ForegroundColor Yellow } else { Write-Host 'Cle API existante conservee.' }"
echo.

REM ================= 4. Systray + demarrage auto =================
echo [4/4] Installation de l'icone systray et du demarrage automatique...

REM Raccourci dans shell:startup pour lancer le tray au login
powershell -NoProfile -Command "$w = New-Object -ComObject WScript.Shell; $s = $w.CreateShortcut([Environment]::GetFolderPath('Startup') + '\TopSolidMCP Tray.lnk'); $s.TargetPath = 'powershell.exe'; $s.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"' + '%~dp0TrayTopSolidMCP.ps1' + '\"'; $s.Save()"

REM Demarrer le tray tout de suite
start "" powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0TrayTopSolidMCP.ps1"

echo.
echo ==========================================================
echo   Installation terminee !
echo   - Le bridge tourne sur le port 8080
echo   - L'icone systray surveille et relance tout seul
echo   - Au prochain demarrage du PC, tout repartira seul
echo ==========================================================
echo.
echo Cette fenetre se ferme dans 10 secondes...
timeout /t 10 >nul