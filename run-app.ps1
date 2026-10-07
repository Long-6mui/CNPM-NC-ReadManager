# Script chạy ReadManager App

Write-Host "=== ReadManager Startup ===" -ForegroundColor Cyan

# Step 1: Start API server
Write-Host "`nStarting API server..." -ForegroundColor Green
$api_process = Start-Process -NoNewWindow -PassThru `
	-WorkingDirectory "src/ReadManager.Api" `
	-FilePath "dotnet" `
	-ArgumentList "run"

Start-Sleep -Seconds 5
Write-Host "API server started (PID: $($api_process.Id))" -ForegroundColor Green

# Step 2: Start Mobile app
Write-Host "`nStarting Mobile app..." -ForegroundColor Green
Set-Location "src/ReadManager.Mobile"
dotnet run -f net9.0-windows10.0.19041.0 --no-build

# Cleanup on exit
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Stop-Process -Id $api_process.Id -Force 2>&1 | Out-Null
Write-Host "Done!" -ForegroundColor Green
