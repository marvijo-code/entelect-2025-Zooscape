#include "GameState.h"
#include "MCTSEngine.h"
#include "SignalRClient.h"
#include <iostream>
#include <memory>
#include <atomic>
#include <signal.h>
#include <fstream>
// Removed JSON dependency for Windows build

class AdvancedMCTSBot {
private:
    std::unique_ptr<MCTSEngine> mctsEngine;
    std::unique_ptr<SignalRClient> signalRClient;
    std::atomic<bool> isRunning;
    std::string botId;
    
    // Configuration
    struct BotConfig {
        std::string serverUrl = "http://localhost:5000";
        std::string hubName = "bothub";
        std::string botToken = "";
        std::string botNickname = "AdvancedMCTSBot";
        
        // MCTS Configuration
        double explorationConstant = 1.414;
        int maxIterations = 10000;
        int maxSimulationDepth = 100;
        int timeLimit = 1000; // milliseconds
        int numThreads = 4;
        
        // Logging
        bool enableLogging = true;
        bool enableHeuristicLogging = false;
        
        void loadFromFile(const std::string& configPath) {
            // Simplified config loading - use defaults for Windows build
            std::cout << "Using default configuration (JSON parsing disabled for Windows build)" << std::endl;
        }
        
        void saveToFile(const std::string& configPath) const {
            // Simplified config saving
            std::ofstream file(configPath);
            if (file.is_open()) {
                file << "# AdvancedMCTSBot Configuration (Simplified)\n";
                file << "serverUrl=" << serverUrl << "\n";
                file << "hubName=" << hubName << "\n";
                file << "botNickname=" << botNickname << "\n";
                file << "explorationConstant=" << explorationConstant << "\n";
                file << "maxIterations=" << maxIterations << "\n";
                file << "maxSimulationDepth=" << maxSimulationDepth << "\n";
                file << "timeLimit=" << timeLimit << "\n";
                file << "numThreads=" << numThreads << "\n";
                file << "enableLogging=" << (enableLogging ? "true" : "false") << "\n";
                file.close();
            }
        }
    } config;
    
public:
    AdvancedMCTSBot() : isRunning(false) {
        // Load configuration
        config.loadFromFile("config.json");
        
        // Initialize MCTS engine
        mctsEngine = std::make_unique<MCTSEngine>(
            config.explorationConstant,
            config.maxIterations,
            config.maxSimulationDepth,
            config.timeLimit,
            config.numThreads
        );
        
        // Initialize SignalR client
        signalRClient = std::make_unique<SignalRClient>(
            config.serverUrl,
            config.hubName
        );
        
        // Set up event handlers
        setupEventHandlers();
        
        std::cout << "AdvancedMCTSBot initialized with simplified configuration" << std::endl;
    }
    
    ~AdvancedMCTSBot() {
        stop();
    }
    
    bool start() {
        if (isRunning.load()) {
            std::cout << "Bot is already running" << std::endl;
            return false;
        }
        
        std::cout << "Starting AdvancedMCTSBot..." << std::endl;
        
        // Connect to SignalR hub
        if (!signalRClient->connect()) {
            std::cerr << "Failed to connect to SignalR hub: " << signalRClient->getLastError() << std::endl;
            return false;
        }
        
        // Join game
        if (!signalRClient->joinGame("default", config.botNickname)) {
            std::cerr << "Failed to join game: " << signalRClient->getLastError() << std::endl;
            return false;
        }
        
        isRunning.store(true);
        std::cout << "AdvancedMCTSBot started successfully" << std::endl;
        
        return true;
    }
    
    void stop() {
        if (!isRunning.load()) {
            return;
        }
        
        std::cout << "Stopping AdvancedMCTSBot..." << std::endl;
        
        isRunning.store(false);
        
        if (signalRClient) {
            signalRClient->leaveGame();
            signalRClient->disconnect();
        }
        
        std::cout << "AdvancedMCTSBot stopped" << std::endl;
    }
    
    void run() {
        if (!start()) {
            return;
        }
        
        std::cout << "AdvancedMCTSBot is running. Press Ctrl+C to stop." << std::endl;
        
        // Main game loop
        while (isRunning.load()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        
        stop();
    }
    
private:
    void setupEventHandlers() {
        // Game state update handler
        signalRClient->setGameStateUpdateHandler([this](const GameState& gameState) {
            handleGameStateUpdate(gameState);
        });
        
        // Game end handler
        signalRClient->setGameEndHandler([this](const std::string& result) {
            handleGameEnd(result);
        });
        
        // Error handler
        signalRClient->setErrorHandler([this](const std::string& error) {
            handleError(error);
        });
    }
    
    void handleGameStateUpdate(const GameState& gameState) {
        if (!isRunning.load()) {
            return;
        }
        
        try {
            if (config.enableLogging) {
                std::cout << "Received game state update" << std::endl;
            }
            
            // Use MCTS to determine best move
            BotAction bestAction = mctsEngine->findBestAction(gameState, "player1");
            
            // Send the move
            if (!signalRClient->sendMove(bestAction)) {
                std::cerr << "Failed to send move: " << signalRClient->getLastError() << std::endl;
            } else if (config.enableLogging) {
                std::cout << "Sent move: " << static_cast<int>(bestAction) << std::endl;
            }
            
        } catch (const std::exception& e) {
            std::cerr << "Error handling game state update: " << e.what() << std::endl;
        }
    }
    
    void handleGameEnd(const std::string& result) {
        std::cout << "Game ended: " << result << std::endl;
        
        // Save configuration
        config.saveToFile("config_output.txt");
        
        // Could implement learning/statistics here
    }
    
    void handleError(const std::string& error) {
        std::cerr << "Game error: " << error << std::endl;
    }
};

// Global bot instance for signal handling
std::unique_ptr<AdvancedMCTSBot> g_bot;

void signalHandler(int signal) {
    std::cout << "\nReceived signal " << signal << ". Shutting down gracefully..." << std::endl;
    if (g_bot) {
        g_bot->stop();
    }
    exit(0);
}

int main(int argc, char* argv[]) {
    // Set up signal handling
    signal(SIGINT, signalHandler);
    signal(SIGTERM, signalHandler);
    
    try {
        std::cout << "=== Advanced MCTS Bot for Zooscape ===" << std::endl;
        std::cout << "Simplified Windows Build (No External Dependencies)" << std::endl;
        std::cout << "=======================================" << std::endl;
        
        // Create and run bot
        g_bot = std::make_unique<AdvancedMCTSBot>();
        g_bot->run();
        
    } catch (const std::exception& e) {
        std::cerr << "Fatal error: " << e.what() << std::endl;
        return 1;
    }
    
    return 0;
}