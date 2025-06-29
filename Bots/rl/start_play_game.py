#!/usr/bin/env python3
"""
Start Play Game with Trained Bot
Launches the Zooscape engine and connects the trained play bot
"""

import subprocess
import sys
import time
import os
import signal

class PlayGameLauncher:
    def __init__(self):
        self.engine_process = None
        self.play_bot_process = None
        self.reference_bot_process = None
        
        # Configuration
        self.engine_exe = "../../engine/Zooscape/bin/Debug/net8.0/Zooscape.exe"
        self.reference_bot_exe = "../../engine/ReferenceBot/bin/Debug/net8.0/ReferenceBot.exe"
        
    def start_engine(self):
        """Start the Zooscape game engine"""
        if not os.path.exists(self.engine_exe):
            print(f"❌ Engine not found at {self.engine_exe}")
            print("Please build the engine first!")
            return False
            
        try:
            print("🚀 Starting Zooscape Engine...")
            self.engine_process = subprocess.Popen(
                [self.engine_exe],
                cwd=os.path.dirname(self.engine_exe),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0
            )
            print("✅ Engine started")
            return True
        except Exception as e:
            print(f"❌ Failed to start engine: {e}")
            return False
    
    def start_reference_bot(self):
        """Start the reference bot as opponent"""
        if not os.path.exists(self.reference_bot_exe):
            print(f"⚠️ Reference bot not found at {self.reference_bot_exe}")
            print("Continuing without reference bot...")
            return True
            
        try:
            print("🤖 Starting Reference Bot...")
            self.reference_bot_process = subprocess.Popen(
                [self.reference_bot_exe],
                cwd=os.path.dirname(self.reference_bot_exe),
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0
            )
            print("✅ Reference Bot started")
            return True
        except Exception as e:
            print(f"⚠️ Failed to start reference bot: {e}")
            print("Continuing without reference bot...")
            return True
    
    def start_play_bot(self):
        """Start the trained play bot"""
        try:
            print("🎯 Starting Trained Play Bot...")
            
            # Use the virtual environment Python
            python_exe = ".venv/Scripts/python.exe" if sys.platform == "win32" else ".venv/bin/python"
            
            self.play_bot_process = subprocess.Popen(
                [python_exe, "play_bot_runner.py"],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
                bufsize=1,
                universal_newlines=True
            )
            print("✅ Play Bot started")
            return True
        except Exception as e:
            print(f"❌ Failed to start play bot: {e}")
            return False
    
    def wait_for_engine(self, timeout=10):
        """Wait for the engine to be ready"""
        print("⏳ Waiting for engine to start...")
        time.sleep(timeout)
        return True
    
    def monitor_processes(self):
        """Monitor the running processes"""
        try:
            print("\n🎮 Game is running! Monitoring processes...")
            print("Press Ctrl+C to stop all processes")
            
            while True:
                time.sleep(1)
                
                # Check if processes are still running
                if self.engine_process and self.engine_process.poll() is not None:
                    print("⚠️ Engine process ended")
                    break
                    
                if self.play_bot_process and self.play_bot_process.poll() is not None:
                    print("⚠️ Play bot process ended")
                    # Try to restart play bot
                    print("🔄 Restarting play bot...")
                    if not self.start_play_bot():
                        break
                
        except KeyboardInterrupt:
            print("\n🛑 Stopping game...")
    
    def cleanup(self):
        """Clean up all processes"""
        print("🧹 Cleaning up processes...")
        
        processes = [
            ("Play Bot", self.play_bot_process),
            ("Reference Bot", self.reference_bot_process),
            ("Engine", self.engine_process)
        ]
        
        for name, process in processes:
            if process:
                try:
                    if sys.platform == "win32":
                        # Use taskkill on Windows
                        subprocess.run(['taskkill', '/F', '/T', '/PID', str(process.pid)],
                                       capture_output=True)
                    else:
                        process.terminate()
                        process.wait(timeout=5)
                    print(f"✅ {name} stopped")
                except Exception as e:
                    print(f"⚠️ Error stopping {name}: {e}")
    
    def run(self):
        """Run the complete game setup"""
        try:
            print("🎮 Starting Zooscape Play Game with Trained Bot")
            print("=" * 50)
            
            # Step 1: Start engine
            if not self.start_engine():
                return False
            
            # Step 2: Wait for engine to be ready
            self.wait_for_engine()
            
            # Step 3: Start reference bot (optional)
            self.start_reference_bot()
            
            # Step 4: Wait a bit more
            time.sleep(2)
            
            # Step 5: Start play bot
            if not self.start_play_bot():
                return False
            
            # Step 6: Monitor everything
            self.monitor_processes()
            
            return True
            
        except Exception as e:
            print(f"❌ Error during game setup: {e}")
            return False
        finally:
            self.cleanup()

def main():
    print("🚀 Zooscape Play Game Launcher")
    print("This will start the game engine and connect your trained bot")
    print()
    
    # Check if we're in the right directory
    if not os.path.exists("play_bot_runner.py"):
        print("❌ Please run this script from the Bots/rl directory")
        return 1
    
    # Check if model exists
    import glob
    model_files = glob.glob("models/zooscape_real_logs_*.weights.h5")
    if not model_files:
        print("❌ No trained model found! Please run training first:")
        print("   python train_with_real_logs.py")
        return 1
    
    latest_model = max(model_files, key=os.path.getctime)
    print(f"🧠 Using trained model: {latest_model}")
    print()
    
    # Launch the game
    launcher = PlayGameLauncher()
    success = launcher.run()
    
    if success:
        print("✅ Game session completed successfully")
        return 0
    else:
        print("❌ Game session failed")
        return 1

if __name__ == "__main__":
    sys.exit(main()) 