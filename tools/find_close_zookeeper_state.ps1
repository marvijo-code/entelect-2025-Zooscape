param(
    [string]$LogDirectory,
    [string]$BotNickname = "StaticHeuro",
    [int]$DistanceThreshold = 4
)

# Get all json files, sorted by tick number
$files = Get-ChildItem -Path $LogDirectory -Filter *.json |
    Where-Object { $_.BaseName -match '^\d+$' } |
    Sort-Object { [int]$_.BaseName }

if ($files.Count -eq 0) {
    Write-Host "No valid log files found in the specified directory."
    exit
}

# Function to calculate Manhattan distance
function Get-ManhattanDistance($pos1, $pos2) {
    return [Math]::Abs($pos1.X - $pos2.X) + [Math]::Abs($pos1.Y - $pos2.Y)
}

# Iterate through each file to check for zookeeper proximity
foreach ($file in $files) {
    $gameState = Get-Content -Raw -Path $file.FullName | ConvertFrom-Json -ErrorAction SilentlyContinue
    if (-not $gameState) {
        continue
    }

    $bot = $gameState.Bots | Where-Object { $_.Nickname -eq $BotNickname }
    $zookeepers = $gameState.Zookeepers

    if ($bot -and $zookeepers) {
        foreach ($zookeeper in $zookeepers) {
            $distance = Get-ManhattanDistance -pos1 $bot.Position -pos2 $zookeeper.Position
            if ($distance -lt $DistanceThreshold) {
                Write-Host "Found state where bot '$BotNickname' is close to a zookeeper (distance: $distance)."
                Write-Host "File: $($file.FullName)"
                Write-Output $file.FullName
                exit
            }
        }
    }
}

Write-Host "No state found where bot '$BotNickname' is within $DistanceThreshold steps of a zookeeper in '$LogDirectory'."
