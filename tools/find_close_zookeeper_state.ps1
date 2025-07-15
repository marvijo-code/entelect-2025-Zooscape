# Script to find a game state where a bot is captured or close to a zookeeper.

param(
    [string]$LogDirectory,
    [string]$BotNickname,
    [int]$DistanceThreshold = 4
)

function Get-ManhattanDistance {
    param($obj1, $obj2)
    return [Math]::Abs($obj1.X - $obj2.X) + [Math]::Abs($obj1.Y - $obj2.Y)
}

# Get all JSON files in the specified directory, sorted by name (which should correspond to tick order)
$gameFiles = Get-ChildItem -Path $LogDirectory -Filter "*.json" | Sort-Object { [int]($_.Name -replace '\.json$') }

if ($gameFiles.Count -eq 0) {
    Write-Warning "No JSON files found in directory '$LogDirectory'."
    exit
}

Write-Host "Searching for bot '$BotNickname' in '$LogDirectory'..."

foreach ($file in $gameFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw | ConvertFrom-Json
    } 
    catch {
        Write-Warning "Could not parse JSON file: $($file.FullName)"
        continue
    }

    # Find the specified bot in the current game state
    $bot = $content.Animals | Where-Object { $_.Nickname -eq $BotNickname }

    if ($null -ne $bot) {
        # --- NEW: Check for capture ---
        if ($bot.CapturedCounter -gt 0) {
            Write-Host "Found state where bot '$BotNickname' was captured."
            Write-Host "File: $($file.FullName)"
            Write-Output $file.FullName
            exit # Exit after finding the first capture event
        }

        # --- ORIGINAL: Check for proximity ---
        $zookeepers = $content.Zookeepers
        if ($null -ne $zookeepers) {
            foreach ($zookeeper in $zookeepers) {
                $distance = Get-ManhattanDistance -obj1 $bot -obj2 $zookeeper
                if ($distance -lt $DistanceThreshold) {
                    Write-Host "Found state where bot '$BotNickname' is close to a zookeeper (distance: $distance)."
                    Write-Host "File: $($file.FullName)"
                    Write-Output $file.FullName
                    exit # Exit after finding the first close proximity event
                }
            }
        }
    }
}

Write-Host "No state found where bot '$BotNickname' is within $DistanceThreshold steps of a zookeeper or has been captured in '$LogDirectory'."
