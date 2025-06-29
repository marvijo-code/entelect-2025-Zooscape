@echo off
echo 🎮 Starting Zooscape Play Game with Trained Bot
echo ===============================================
echo.

REM Check if we're in the right directory
if not exist "play_bot_runner.py" (
    echo ❌ Please run this script from the Bots/rl directory
    pause
    exit /b 1
)

REM Check if model exists
if not exist "models\zooscape_real_logs_*.weights.h5" (
    echo ❌ No trained model found! Please run training first:
    echo    .venv\Scripts\python.exe train_with_real_logs.py
    pause
    exit /b 1
)

echo 🧠 Found trained model ready for play!
echo.

REM Activate virtual environment and run
.venv\Scripts\python.exe start_play_game.py

echo.
echo 🛑 Game session ended
pause 