# RL Bot - Reinforcement Learning Bot for Zooscape

## Overview
The RL Bot is a reinforcement learning agent designed to play the Zooscape game using deep Q-learning. It connects to the game engine via SignalR and learns to maximize its score through experience.

## Game Understanding
Zooscape is similar to Pac-Man where:
- **Animals** (like our RL bot) move around a 51x51 grid-based map
- **Zookeepers** chase the animals (like ghosts in Pac-Man)
- **Pellets** need to be collected for points
- **Power-ups** provide special abilities:
  - `PowerPellet` - Temporary invincibility
  - `ChameleonCloak` - Stealth mode
  - `Scavenger` - Enhanced pellet collection
  - `BigMooseJuice` - Speed boost
- The map has **walls** and different cell types

## What the RL Bot Does

### 1. Game Connection
- Connects to the game engine via SignalR at `ws://localhost:5000/bothub`
- Registers itself as "RLBot" to participate in games

### 2. Real-time Decision Making
- Receives game states containing:
  - Current map layout (51x51 grid)
  - Animal positions (including itself and opponents)
  - Zookeeper positions 
  - Pellet locations
  - Available power-ups
- Uses its neural network to decide actions:
  - `Up, Down, Left, Right` - movement
  - `UseItem` - activate collected power-ups

### 3. Learning Objective
**The RL bot optimizes for score per tick (score/ticks) to achieve the highest absolute score.**

- **Primary Goal**: Maximize total score
- **Efficiency Metric**: Score per tick (score/ticks)
- **Reward Function**: Based on the bot's current score from the game engine
- **Learning**: Adjusts neural network weights based on score outcomes

### 4. Reinforcement Learning Process
- Gets rewards for collecting pellets and achieving high scores
- Gets penalties for being captured by zookeepers or low performance
- Uses experience replay to learn from past games
- Balances exploration vs exploitation with epsilon-greedy strategy

## Files Structure

- `fixed_rl_agent.py` - Main RL agent with DQN implementation
- `training_bot_runner.py` - SignalR client for live training
- `live_training.py` - Training orchestration script
- `test_rl_bot.py` - pytest test suite
- `python_caller.py` - Bridge for C# integration
- `requirements.txt` - Python dependencies

## Training

### Live Training
```bash
python live_training.py [duration_hours]
```
This starts:
1. Zooscape game engine
2. Reference bot (opponent)
3. RL bot training process
4. Monitoring and plotting

### Testing
```bash
pytest test_rl_bot.py -v
```

## Key Features

- **Deep Q-Network (DQN)** with experience replay
- **Epsilon-greedy exploration** strategy
- **Live training** against other bots
- **Score optimization** for competitive play
- **SignalR integration** for real-time gameplay
- **Comprehensive testing** with mock game states

## Performance Metrics

The bot tracks:
- Total score achieved
- Score per tick (efficiency)
- Pellets collected
- Games won/lost
- Average inference time (<150ms requirement)
- Exploration rate (epsilon decay)

## Competitive Strategy

The RL bot learns to:
1. **Maximize score efficiency** (score/ticks)
2. **Collect pellets** strategically
3. **Avoid zookeepers** to prevent capture
4. **Use power-ups** optimally
5. **Adapt** to different opponents and map layouts 