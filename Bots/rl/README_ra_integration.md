# RL Bot Integration with PowerShell Runner

## Overview
The RL Play Bot has been integrated into the main `ra-run-all-local.ps1` script for automated multi-bot gameplay and scoring.

## Configuration
The script now includes:
- **RLPlayBot**: Uses trained model for intelligent gameplay
- **ClingyHeuroBot2**: Advanced heuristic bot 
- **ReferenceBot**: Baseline comparison bot
- **ClingyHeuroBot**: Another heuristic bot

## Usage

### 1. Ensure Training Complete
Make sure you have a trained model:
```bash
cd Bots/rl
ls models/*.weights.h5  # Should show trained models
```

### 2. Run Multi-Bot Game
From the project root:
```powershell
.\ra-run-all-local.ps1
```

This will:
- ✅ Build the Zooscape engine
- ✅ Check Python virtual environment
- ✅ Launch all bots in separate Windows Terminal tabs
- ✅ Restart games every 3 minutes automatically
- ✅ Log performance and scores

### 3. Monitor Performance
Each bot runs in its own colored tab:
- **Engine**: Blue tab - Game orchestration
- **RLPlayBot**: Green tab - Your trained AI
- **ClingyHeuroBot2**: Red tab - Advanced heuristic
- **ReferenceBot**: Purple tab - Baseline bot
- **ClingyHeuroBot**: Orange tab - Heuristic bot

### 4. Controls
In the main PowerShell window:
- `q` - Stop all bots (tabs remain open for log review)
- `Enter` (after stopping) - Restart all bots
- `c` - Close script, leave bots running
- Games auto-restart every 3 minutes

## What Happens
1. **Engine starts** on port 5000
2. **All bots connect** via SignalR
3. **Games run automatically** with score tracking
4. **Results logged** in each bot's terminal tab
5. **Continuous gameplay** with periodic restarts

## Viewing Results
- Each bot's tab shows real-time logs
- Scores are displayed as games progress  
- Engine tab shows overall game state
- Compare your RL bot's performance vs others

## Troubleshooting
- Ensure `.venv` exists in `Bots/rl/`
- Verify trained model files in `models/`
- Check port 5000 is available
- All bots must build successfully first

Your trained RL bot will now compete automatically against multiple opponents! 🎯 