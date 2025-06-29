@echo off
echo Starting Zooscape Real Logs Training...
echo.

REM Activate virtual environment and run training
.venv\Scripts\python.exe train_with_real_logs.py --episodes_per_log 3

echo.
echo Training completed! Check the models directory for results.
pause 