# ==============================================================================
# IlyPhoto Website - Run Script for Antigravity IDE
# ==============================================================================

$ErrorActionPreference = "Stop"

# Detect .NET 9 SDK
$userSdkPath = "$env:LOCALAPPDATA\Microsoft\dotnet"
if (Test-Path "$userSdkPath\dotnet.exe") {
    $env:DOTNET_ROOT = $userSdkPath
    $env:PATH = "$userSdkPath;$env:PATH"
    $dotnetExe = "$userSdkPath\dotnet.exe"
} else {
    $dotnetExe = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnetExe) {
        Write-Error "No .NET SDK found. Please ensure .NET 9 SDK is installed."
        exit 1
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       IlyPhoto NGO Photography Platform (Antigravity IDE)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " .NET SDK   : $(& $dotnetExe --version)" -ForegroundColor Yellow
Write-Host " Solution   : IlyPhoto.sln" -ForegroundColor Yellow
Write-Host " Website URL: http://localhost:5239" -ForegroundColor Green
Write-Host " Health API : http://localhost:5239/api/health" -ForegroundColor Green
Write-Host " Diagnostics: http://localhost:5239/Diagnostics" -ForegroundColor Green
Write-Host "----------------------------------------------------------" -ForegroundColor DarkGray
Write-Host " Default Accounts:" -ForegroundColor White
Write-Host "   Admin  : admin@ilyphoto.co.za / AdminPass123!" -ForegroundColor White
Write-Host "   Client : client@test.com      / ClientPass123!" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Starting server... (Press Ctrl+C to stop)`n" -ForegroundColor Green

# Automatically open default browser after server initializes
Start-Job -ScriptBlock {
    Start-Sleep -Seconds 2
    Start-Process "http://localhost:5239"
} | Out-Null

& $dotnetExe run --project IlyPhotoWeb\IlyPhotoWeb.csproj --urls "http://localhost:5239"
