
param(
    [Parameter(Mandatory=$true)]
    [string]$GameStateFile,
    
    [Parameter(Mandatory=$true)]
    [string]$TestName,
    
    [Parameter(Mandatory=$false)]
    [string]$BotNicknameInState = $null,
    
    [Parameter(Mandatory=$false)]
    [string[]]$BotsToTest = @("ClingyHeuroBot2"),
    
    [Parameter(Mandatory=$false)]
    [string]$Description = "Automated test",
    
    [Parameter(Mandatory=$false)]
    [int[]]$AcceptableActions = @(1, 2, 3, 4), # Up, Down, Left, Right by default
    
    [Parameter(Mandatory=$false)]
    [string]$TestType = "SingleBot",
    
    [Parameter(Mandatory=$false)]
    [bool]$TickOverride = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5008/api/test/create",
    
    [Parameter(Mandatory=$false)]
    [string]$GameStatesDir = "FunctionalTests/GameStates"
)

# Validate that the game state file exists
$gameStateFilePath = Join-Path $GameStatesDir $GameStateFile
if (-not (Test-Path $gameStateFilePath)) {
    Write-Error "Game state file not found: $gameStateFilePath"
    exit 1
}

# Read the game state JSON
try {
    $gameStateJson = Get-Content $gameStateFilePath -Raw
    Write-Host "Loaded game state file: $gameStateFilePath" -ForegroundColor Green
} catch {
    Write-Error "Failed to read game state file: $_"
    exit 1
}

# Convert acceptable actions array to JSON format
$acceptableActionsJson = ($AcceptableActions | ForEach-Object { [int]$_ }) -join ", "

# Convert bots array to JSON format
$botsToTestJson = ($BotsToTest | ForEach-Object { "`"$_`"" }) -join ", "

# Handle bot nickname (can be null)
$botNicknameJson = if ($BotNicknameInState) { "`"$BotNicknameInState`"" } else { "null" }

# Create the request body
$body = @"
{
    "TestName": "$TestName",
    "GameStateFile": "$GameStateFile",
    "CurrentGameState": $gameStateJson,
    "TestType": "$TestType",
    "BotNicknameInState": $botNicknameJson,
    "BotsToTest": [$botsToTestJson],
    "Description": "$Description",
    "TickOverride": $TickOverride,
    "AcceptableActions": [$acceptableActionsJson]
}
"@

try {
    Write-Host "Creating test: $TestName" -ForegroundColor Yellow
    Write-Host "Bot Nickname in State: $(if ($BotNicknameInState) { $BotNicknameInState } else { 'null' })" -ForegroundColor Cyan
    Write-Host "Bots to Test: [$($BotsToTest -join ', ')]" -ForegroundColor Cyan
    Write-Host "Game State: $GameStateFile" -ForegroundColor Cyan
    Write-Host "Acceptable Actions: [$acceptableActionsJson]" -ForegroundColor Cyan
    
    $response = Invoke-RestMethod -Uri $ApiUrl -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "✅ Test created successfully!" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json -Depth 2)" -ForegroundColor Gray
    
} catch {
    Write-Error "Failed to create test: $_"
    Write-Host "Request summary:" -ForegroundColor Red
    Write-Host "  TestName: $TestName" -ForegroundColor Red
    Write-Host "  GameStateFile: $GameStateFile" -ForegroundColor Red
    Write-Host "  TestType: $TestType" -ForegroundColor Red
    Write-Host "  BotNicknameInState: $(if ($BotNicknameInState) { $BotNicknameInState } else { 'null' })" -ForegroundColor Red
    Write-Host "  BotsToTest: [$($BotsToTest -join ', ')]" -ForegroundColor Red
    Write-Host "  AcceptableActions: [$acceptableActionsJson]" -ForegroundColor Red
    exit 1
} 