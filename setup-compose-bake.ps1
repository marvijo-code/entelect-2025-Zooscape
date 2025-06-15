# Create PowerShell profile if it doesn't exist
if (-not (Test-Path -Path $PROFILE)) {
    New-Item -ItemType File -Path $PROFILE -Force
}

# Add COMPOSE_BAKE setting
$bakeSetting = '$env:COMPOSE_BAKE = "true"'

if (-not (Get-Content $PROFILE | Select-String -Pattern 'COMPOSE_BAKE')) {
    Add-Content -Path $PROFILE -Value "`n# Enable Docker Compose bake optimization`n$bakeSetting"
    Write-Host "✅ COMPOSE_BAKE=true added to your PowerShell profile" -ForegroundColor Green
}
else {
    Write-Host "ℹ️ COMPOSE_BAKE setting already exists in your profile" -ForegroundColor Yellow
}

Write-Host "Restart PowerShell for changes to take effect" -ForegroundColor Cyan
