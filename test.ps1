# ==============================================================================
# IlyPhoto Website - Test Runner for Antigravity IDE
# ==============================================================================

$ErrorActionPreference = "Stop"

$userSdkPath = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$userSdkPath\dotnet.exe") {
    $env:DOTNET_ROOT = $userSdkPath
    $env:PATH = "$userSdkPath;$env:PATH"
    $dotnetExe = "$userSdkPath\dotnet.exe"
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnetExe) {
        Write-Error "No .NET SDK found."
        exit 1
    }
}

Write-Host "Running IlyPhoto test suite..." -ForegroundColor Cyan
& $dotnetExe test IlyPhotoWeb.Tests\IlyPhotoWeb.Tests.csproj --logger "console;verbosity=normal"
