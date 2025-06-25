#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Game State Inspector - Analyze JSON game state files for bot behavior debugging

.DESCRIPTION
    This script provides a convenient wrapper around the Game State Inspector tool
    to analyze game state JSON files and understand bot decision-making context.

.PARAMETER GameStateFile
    Path to the JSON game state file to analyze (relative to project root)

.PARAMETER BotNickname
    Nickname of the bot to analyze in the game state

.PARAMETER ShowHelp
    Display usage examples and help information

.EXAMPLE
    .\inspect-game-state.ps1 -GameStateFile "FunctionalTests/GameStates/12.json" -BotNickname "ClingyHeuroBot2"
    
.EXAMPLE
    .\inspect-game-state.ps1 -GameStateFile "FunctionalTests/GameStates/162.json" -BotNickname "SomeBot"

.EXAMPLE
    .\inspect-game-state.ps1 -ShowHelp
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$GameStateFile,
    
    [Parameter(Mandatory=$false)]
    [string]$BotNickname,
    
    [Parameter(Mandatory=$false)]
    [switch]$ShowHelp
)

function Show-Help {
    Write-Host "=== Game State Inspector ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage Examples:" -ForegroundColor Yellow
    Write-Host "  .\inspect-game-state.ps1 -GameStateFile 'FunctionalTests/GameStates/12.json' -BotNickname 'ClingyHeuroBot2'"
    Write-Host "  .\inspect-game-state.ps1 -GameStateFile 'FunctionalTests/GameStates/162.json' -BotNickname 'SomeBot'"
    Write-Host ""
    Write-Host "Available Game State Files:" -ForegroundColor Yellow
    $gameStatesPath = "../../FunctionalTests/GameStates"
    if (Test-Path $gameStatesPath) {
        Get-ChildItem -Path $gameStatesPath -Filter "*.json" | ForEach-Object {
            Write-Host "  - FunctionalTests/GameStates/$($_.Name)"
        }
    } else {
        Write-Host "  (GameStates directory not found - run from tools/GameStateInspector/)"
    }
    Write-Host ""
    Write-Host "Common Bot Nicknames:" -ForegroundColor Yellow
    Write-Host "  - ClingyHeuroBot2"
    Write-Host "  - ClingyHeuroBot"
    Write-Host "  - MCTSo4"
    Write-Host "  - AdvancedMCTSBot"
    Write-Host ""
}

function Test-Prerequisites {
    # Check if we're in the right directory
    if (-not (Test-Path "GameStateInspector.csproj")) {
        Write-Error "Error: Must run from tools/GameStateInspector/ directory"
        Write-Host "Current directory: $(Get-Location)"
        Write-Host "Expected files: GameStateInspector.csproj, Program.cs"
        return $false
    }
    
    # Check if dotnet is available
    try {
        $null = Get-Command dotnet -ErrorAction Stop
    } catch {
        Write-Error "Error: .NET CLI (dotnet) not found. Please install .NET 8.0 or later."
        return $false
    }
    
    return $true
}

function Invoke-GameStateInspector {
    param(
        [string]$JsonFile,
        [string]$Bot
    )
    
    # Convert relative path to absolute if needed
    $fullPath = $JsonFile
    if (-not [System.IO.Path]::IsPathRooted($JsonFile)) {
        $fullPath = Join-Path (Get-Location) "../../$JsonFile"
    }
    
    # Check if file exists
    if (-not (Test-Path $fullPath)) {
        Write-Error "Error: Game state file not found: $fullPath"
        Write-Host ""
        Write-Host "Available files in FunctionalTests/GameStates/:"
        $gameStatesPath = "../../FunctionalTests/GameStates"
        if (Test-Path $gameStatesPath) {
            Get-ChildItem -Path $gameStatesPath -Filter "*.json" | ForEach-Object {
                Write-Host "  - $($_.Name)"
            }
        }
        return
    }
    
    Write-Host "=== Running Game State Inspector ===" -ForegroundColor Green
    Write-Host "File: $JsonFile"
    Write-Host "Bot: $Bot"
    Write-Host ""
    
    # Run the inspector
    dotnet run -- $fullPath $Bot
}

# Main execution
if ($ShowHelp -or (-not $GameStateFile) -or (-not $BotNickname)) {
    Show-Help
    exit 0
}

if (-not (Test-Prerequisites)) {
    exit 1
}

Invoke-GameStateInspector -JsonFile $GameStateFile -Bot $BotNickname 