# Examples of using the generalized create_test.ps1 script

# Example 1: Basic test creation (minimal parameters)
Write-Host "Example 1: Basic test creation" -ForegroundColor Yellow
.\create_test.ps1 -GameStateFile "tick_1100.json" -TestName "Example_Basic_Test"

# Example 2: Test with specific bot nickname in state and bots to test
Write-Host "`nExample 2: Test with specific bot nickname and bots to test" -ForegroundColor Yellow
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "Example_StaticHeuro_Test" `
    -BotNicknameInState "StaticHeuro" `
    -BotsToTest @("StaticHeuro") `
    -Description "Test StaticHeuro bot decision making in complex scenario"

# Example 3: Test with limited acceptable actions (only Up and Left)
Write-Host "`nExample 3: Test with limited acceptable actions" -ForegroundColor Yellow
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "Example_Limited_Actions" `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(1, 3) `
    -Description "Bot should choose between Up (1) or Left (3) only"

# Example 4: Test with tick override
Write-Host "`nExample 4: Test with tick override" -ForegroundColor Yellow
.\create_test.ps1 `
    -GameStateFile "7.json" `
    -TestName "Example_Tick_Override" `
    -BotNicknameInState "ClingyHeuroBot" `
    -BotsToTest @("ClingyHeuroBot") `
    -TickOverride $true `
    -Description "Test with tick override functionality enabled"

# Example 5: Test with multiple bots to test
Write-Host "`nExample 5: Test with multiple bots to test" -ForegroundColor Yellow
.\create_test.ps1 `
    -GameStateFile "34.json" `
    -TestName "Example_Multi_Bot" `
    -BotsToTest @("ClingyHeuroBot2", "ClingyHeuroBot", "StaticHeuro") `
    -TestType "MultiBotArray" `
    -Description "Test comparing multiple bots on same game state"

# Example 6: Generate timestamp-based test name
Write-Host "`nExample 6: Generate timestamp-based test name" -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$tickNumber = "1100"
$testName = "AutoGen_${timestamp}_${tickNumber}_Analysis"

.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName $testName `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(1, 3, 2) `
    -Description "Automated test for tick $tickNumber - ClingyHeuroBot2 strategic analysis"

# Example 7: Batch test creation for multiple bots on same game state
Write-Host "`nExample 7: Batch test creation for multiple bots" -ForegroundColor Yellow
$bots = @("ClingyHeuroBot2", "ClingyHeuroBot", "StaticHeuro")
$gameState = "162.json"
$baseTimestamp = Get-Date -Format "yyyyMMdd_HHmm"

foreach ($bot in $bots) {
    $testName = "BatchTest_${baseTimestamp}_${bot}"
    Write-Host "Creating test for $bot..." -ForegroundColor Cyan
    
    .\create_test.ps1 `
        -GameStateFile $gameState `
        -TestName $testName `
        -BotNicknameInState $bot `
        -BotsToTest @($bot) `
        -Description "Batch test comparing $bot performance on complex game state"
}

# Example 8: Test without specifying bot nickname (uses first animal in state)
Write-Host "`nExample 8: Test without bot nickname (uses first animal)" -ForegroundColor Yellow
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "Example_No_Nickname" `
    -BotsToTest @("ClingyHeuroBot2") `
    -Description "Test using first animal in game state"

Write-Host "`n✅ All example tests completed!" -ForegroundColor Green

# Usage instructions
Write-Host "`n📋 Usage Instructions:" -ForegroundColor Magenta
Write-Host "Required parameters:" -ForegroundColor White
Write-Host "  -GameStateFile: Name of the JSON file in FunctionalTests/GameStates/" -ForegroundColor Gray
Write-Host "  -TestName: Unique name for the test" -ForegroundColor Gray
Write-Host ""
Write-Host "Optional parameters:" -ForegroundColor White
Write-Host "  -BotNicknameInState: Nickname of bot in the game state (default: null - uses first animal)" -ForegroundColor Gray
Write-Host "  -BotsToTest: Array of bot names to test (default: @('ClingyHeuroBot2'))" -ForegroundColor Gray
Write-Host "  -Description: Test description (default: 'Automated test')" -ForegroundColor Gray
Write-Host "  -AcceptableActions: Array of action IDs (default: @(1,2,3,4))" -ForegroundColor Gray
Write-Host "    • Up: 1, Down: 2, Left: 3, Right: 4" -ForegroundColor DarkGray
Write-Host "  -TestType: Type of test (default: 'SingleBot')" -ForegroundColor Gray
Write-Host "  -TickOverride: Enable tick override (default: false)" -ForegroundColor Gray
Write-Host "  -ApiUrl: API endpoint (default: http://localhost:5008/api/test/create)" -ForegroundColor Gray
Write-Host "  -GameStatesDir: Directory containing game states (default: FunctionalTests/GameStates)" -ForegroundColor Gray 