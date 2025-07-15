param(
    [string]$LogDirectory,
    [string]$BotNickname
)

$files = Get-ChildItem -Path $LogDirectory -Filter *.json

foreach ($file in $files) {
    $tick = 0
    try {
        $tick = [int]$file.BaseName
    } catch {
        # Could not convert to int, skip this file
        continue
    }

    if ($tick -ge 50) {
        $filePath = $file.FullName
        Write-Host "Analyzing file: $filePath"
        $output = dotnet run --project tools/GameStateInspector -- "$filePath" "$BotNickname" | Out-String

        $match = $output | Select-String -Pattern "Nearest Zookeeper: (\d+) steps away"

        if ($match) {
            $distance = [int]$match.Matches[0].Groups[1].Value
            if ($distance -lt 4) {
                Write-Host "Found suitable game state: $filePath with zookeeper at distance $distance"
                # Output the file path for the calling script to capture
                Write-Output $filePath
                break
            }
        }
    }
}
