@echo off
chcp 65001 >nul
title TopSolid MCP Bridge
cd /d "%~dp0bridge"

echo ============================================
echo   TopSolid MCP - Demarrage du bridge HTTP
echo ============================================
echo.

REM --- Trouver node.js (priorite: node du PATH, sinon installation typique) ---
set "NODE=node"
where node >nul 2>&1
if errorlevel 1 (
    if exist "%ProgramFiles%\nodejs\node.exe" set "NODE=%ProgramFiles%\nodejs\node.exe"
)

REM --- Verifier que le bridge est installe (node_modules present) ---
if not exist "node_modules\mcp-proxy" (
    echo [1/3] Installation des dependances ^(une seule fois^)...
    call npm install
    if errorlevel 1 (
        echo ERREUR: npm install a echoue. Verifie ta connexion internet.
        pause
        exit /b 1
    )
    echo.
)

REM --- Charger la cle API (env var utilisateur d'abord, puis config Hermes si presente) ---
set "APIKEY="
for /f "usebackq delims=" %%K in (`powershell -NoProfile -Command "[Environment]::GetEnvironmentVariable('TOPSOLID_MCP_API_KEY','User')"`) do set "APIKEY=%%K"
if "%APIKEY%"=="" if exist "N:\Noemid_System\hermes\config.yaml" (
    for /f "usebackq delims=" %%K in (`powershell -NoProfile -Command "(Get-Content 'N:\Noemid_System\hermes\config.yaml' | Select-String 'X-API-Key').Line.Split(':')[1].Trim()"`) do set "APIKEY=%%K"
)
if "%APIKEY%"=="" (
    echo ATTENTION: pas de cle API trouvee - le bridge refusera de demarrer en mode -Open.
    echo Fix : powershell -Command "[Environment]::SetEnvironmentVariable('TOPSOLID_MCP_API_KEY','<ta-cle>','User')"
    echo.
)

REM --- Lancer le bridge en mode ouvert ^(accessible depuis le reseau Tailscale^) ---
echo [2/3] Demarrage du bridge sur le port 8080...
set "TOPSOLID_MCP_API_KEY=%APIKEY%"
powershell -NoProfile -ExecutionPolicy Bypass -File "start-bridge.ps1" -Open
if errorlevel 1 (
    echo.
    echo ERREUR: le bridge n'a pas demarre. Lis le message ci-dessus.
    echo Fenetre fermee dans 10 secondes...
    timeout /t 10 >nul
    exit /b 1
)