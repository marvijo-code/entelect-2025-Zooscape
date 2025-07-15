
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
    [string]$AcceptableActions = "1,2,3,4", # Default: Up, Down, Left, Right
    
    [Parameter(Mandatory=$false)]
    [string]$TestType = "SingleBot",
    
    [Parameter(Mandatory=$false)]
    [bool]$TickOverride = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$ApiUrl = "http://localhost:5008/api/test/create",
    
    [Parameter(Mandatory=$false)]
    [string]$GameStatesDir = "FunctionalTests/GameStates"
)

# Parse the AcceptableActions string into an integer array
$AcceptableActionsArray = $AcceptableActions.Split(',') | ForEach-Object { [int]$_.Trim() }

# Read the game state JSON
try {
    $gameStateFilePath = Join-Path $GameStatesDir $GameStateFile
    $gameStateJson = Get-Content $gameStateFilePath -Raw
    Write-Host "Loaded game state file: $gameStateFilePath" -ForegroundColor Green
} catch {
    Write-Error "Failed to read game state file: $_"
    exit 1
}

# Create a PowerShell object representing the request body
$requestObject = [PSCustomObject]@{ 
    TestName = $TestName
    GameStateFile = $GameStateFile
    CurrentGameState = ($gameStateJson | ConvertFrom-Json)
    TestType = $TestType
    BotsToTest = $BotsToTest
    Description = $Description
    TickOverride = $TickOverride
    AcceptableActions = $AcceptableActionsArray # Pass the parsed integer array
}

# Add BotNicknameInState only if it's provided
if ($BotNicknameInState) {
    $requestObject | Add-Member -MemberType NoteProperty -Name "BotNicknameInState" -Value $BotNicknameInState
}

# Convert the entire object to a JSON string
$body = $requestObject | ConvertTo-Json -Depth 10

try {
    Write-Host "Creating test: $TestName" -ForegroundColor Yellow
    Write-Host "Bot Nickname in State: $(if ($BotNicknameInState) { $BotNicknameInState } else { 'null' })" -ForegroundColor Cyan
    Write-Host "Bots to Test: [$($BotsToTest -join ', ')]" -ForegroundColor Cyan
    Write-Host "Game State: $GameStateFile" -ForegroundColor Cyan
    Write-Host "Acceptable Actions: [$($AcceptableActions -join ', ')]" -ForegroundColor Cyan
    
    $response = Invoke-WebRequest -Uri $ApiUrl -Method POST -Body $body -ContentType "application/json"

    # Check response and provide feedback
    if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 201 -or $response.StatusCode -eq 204)
    {
        Write-Host "Test created successfully! (HTTP $($response.StatusCode))" -ForegroundColor Green
        $content = $response.Content | ConvertFrom-Json
        Write-Host "Response: $($content | ConvertTo-Json -Depth 5)" -ForegroundColor Gray
    }
    else
    {
        Write-Host "Failed to create test. Status Code: $($response.StatusCode)" -ForegroundColor Red
        Write-Host "Response Content: $($response.Content)" -ForegroundColor Red
        Write-Host "Request summary:" -ForegroundColor Red
        Write-Host "  TestName: $TestName" -ForegroundColor Red
        Write-Host "  GameStateFile: $GameStateFile" -ForegroundColor Red
        Write-Host "  TestType: $TestType" -ForegroundColor Red
        Write-Host "  BotNicknameInState: $(if ($BotNicknameInState) { $BotNicknameInState } else { 'null' })" -ForegroundColor Red
        Write-Host "  BotsToTest: [$($BotsToTest -join ', ')]" -ForegroundColor Red
        Write-Host "  AcceptableActions: [$($AcceptableActions -join ', ')]" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Error "Failed to create test: $_"
    exit 1
}