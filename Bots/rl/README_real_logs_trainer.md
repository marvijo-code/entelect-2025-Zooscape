# Zooscape Real Logs Trainer

This directory now contains a comprehensive offline training system that uses **real game log data** from the `/logs` directory to train RL models.

## Files

- `train_with_real_logs.py` - Main offline trainer using real game logs
- `fixed_rl_agent.py` - Core RL agent implementation with DQN
- `live_training.py` - Live training orchestrator 
- `training_bot_runner_signalrcore.py` - Bot client for live training

## Real Logs Training

The offline trainer (`train_with_real_logs.py`) processes actual game log data from previous matches to train the RL model.

### Features

- **Real Data Processing**: Reads JSON game state files from log directories
- **State Reconstruction**: Converts game logs into training states with proper map data
- **Action Inference**: Infers actions taken based on position changes
- **Reward Calculation**: Calculates rewards based on score improvements and game outcomes
- **Model Training**: Uses DQN with CNN layers for grid processing
- **Progress Tracking**: Saves training metrics, plots, and model checkpoints

### Usage

```bash
# Basic training with real logs
.venv/Scripts/python.exe train_with_real_logs.py

# Custom parameters
.venv/Scripts/python.exe train_with_real_logs.py \
    --logs_dir ../../logs \
    --episodes_per_log 3 \
    --model_dir models

# Help
.venv/Scripts/python.exe train_with_real_logs.py --help
```

### Parameters

- `--logs_dir`: Directory containing game logs (default: `../../logs`)
- `--episodes_per_log`: Number of training episodes per log directory (default: 3)
- `--model_dir`: Directory to save trained models (default: `models`)

### Training Process

1. **Log Discovery**: Scans for date-based log directories (e.g., `20250625_191854`)
2. **Data Processing**: Each directory contains JSON files with game states by tick
3. **Episode Creation**: Processes game sequences multiple times for training variation
4. **Model Training**: Uses DQN with experience replay and target networks
5. **Output**: Saves trained model, metrics, and visualization plots

### Real Training Results Example

```
Found 123 game log directories

Processing log directory: 20250616_114729
Found 395 game states
Episode 1/1
Episode reward: 378.54, Length: 394, Epsilon: 0.093

Processing log directory: 20250616_115817
Found 2000 game states
Episode 1/1
Episode reward: 2818.98, Length: 1999, Epsilon: 0.057

Training Summary:
Average Reward: 980.22
Max Reward: 2818.98
Average Score: 83126.40
Max Score: 255348.00
Final Epsilon: 0.033
```

### Generated Files

After training, the following files are created in the `models/` directory:

- `zooscape_real_logs_TIMESTAMP.weights.h5` - Trained model weights
- `training_metrics_TIMESTAMP.json` - Training statistics
- `training_plots_TIMESTAMP.png` - Visualization plots

### Game Log Format

The trainer processes JSON files with this structure:

```json
{
  "Tick": 900,
  "Cells": [
    {"X": 0, "Y": 0, "Content": 1},
    {"X": 0, "Y": 1, "Content": 2},
    ...
  ],
  "Animals": [
    {
      "Id": "...",
      "Nickname": "BotName",
      "X": 25, "Y": 25,
      "Score": 125087,
      "IsViable": true
    }
  ],
  "Zookeepers": [...]
}
```

### Integration with Live Training

Models trained offline can be loaded in the live training system by updating the model path in the live training configuration.

## Live Training

For real-time training against live opponents:

```bash
.venv/Scripts/python.exe live_training.py
```

This starts the full training environment with:
- Game engine (C# ASP.NET Core)
- RL training bot (Python with SignalR)
- Reference opponent bot
- Training orchestration and monitoring

## Requirements

Ensure the virtual environment is activated and all dependencies are installed:

```bash
# Windows
.venv/Scripts/activate.bat

# Install requirements
pip install -r requirements.txt
```

## Virtual Environment

The training system uses a Python virtual environment located at `.venv/` with all required dependencies including:
- TensorFlow
- NumPy 
- Matplotlib
- SignalR client libraries

## Next Steps

1. **Run Offline Training**: Use real game logs to train initial models
2. **Test Models**: Evaluate trained models against reference bots
3. **Live Training**: Use live training for fine-tuning and adaptation
4. **Model Comparison**: Compare offline vs live trained models

The combination of offline training on historical data and live training provides a comprehensive approach to developing competitive Zooscape bots. 