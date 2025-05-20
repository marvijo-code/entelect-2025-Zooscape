# PowerShell script to run the RL bot training and evaluation
# Navigate to the bots/rl directory and execute the training script

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$rlBotPath = Join-Path -Path $scriptPath -ChildPath "Bots\rl"

# Change to the RL bot directory
Set-Location -Path $rlBotPath

# Run the training and evaluation script
python train_and_evaluate.py

# Return to the original directory
Set-Location -Path $scriptPath
