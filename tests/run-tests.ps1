# MCP Test Suite Runner
# Usage: .\run-tests.ps1 [-UpdateBaselines] [-McpServer <path>] [-Debug]

param(
    [switch]$UpdateBaselines,
    [string]$McpServer = "",
    [switch]$Debug
)

$ErrorActionPreference = "Stop"
$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$runnerExe  = Join-Path $scriptDir "bin\Debug\net48\McpTestRunner.exe"

# Default MCP server path: $env:TOPSOLID_MCP_EXE, else the repo-local Debug
# build. Since the restructuring the server lives under server/src/, not src/.
if ($McpServer -eq "") {
    if ($env:TOPSOLID_MCP_EXE) {
        $McpServer = $env:TOPSOLID_MCP_EXE
    } else {
        $McpServer = Join-Path $scriptDir "..\server\src\bin\Debug\net48\TopSolidMcpServer.exe"
    }
}

# Build test runner if not present or if source is newer
$csproj = Join-Path $scriptDir "McpTestRunner.csproj"
$needBuild = -not (Test-Path $runnerExe)
if (-not $needBuild -and (Test-Path $runnerExe)) {
    $srcFiles = Get-ChildItem $scriptDir -Filter "*.cs" -Recurse
    foreach ($f in $srcFiles) {
        if ($f.LastWriteTime -gt (Get-Item $runnerExe).LastWriteTime) {
            $needBuild = $true; break
        }
    }
}

if ($needBuild) {
    Write-Host "Building McpTestRunner..." -ForegroundColor Cyan
    dotnet build $csproj --configuration Debug --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed."
        exit 1
    }
}

if (-not (Test-Path $McpServer)) {
    Write-Error "TopSolidMcpServer.exe not found at: $McpServer`nBuild it first with: dotnet build server\src\TopSolidMcpServer.csproj`nOr point -McpServer / `$env:TOPSOLID_MCP_EXE at an existing build."
    exit 1
}

Write-Host ""
Write-Host "MCP Server : $McpServer" -ForegroundColor DarkGray
Write-Host "Test Runner: $runnerExe"  -ForegroundColor DarkGray
Write-Host ""

$runnerArgs = @($McpServer)
if ($UpdateBaselines) { $runnerArgs += "--update-baselines" }

& $runnerExe @runnerArgs
exit $LASTEXITCODE
