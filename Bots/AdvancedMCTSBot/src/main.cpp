#include "GameState.h"
#include "MCTSEngine.h"
#include "SignalRClient.h"
#include <iostream>
#include <memory>
#include <atomic>
#include <signal.h>
#include <fstream>
#include <thread>
#include <chrono>
#include <cstdlib>
#include <random>
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
        
        std::string generateGuid() {
            // Simple GUID generation for Windows
            std::random_device rd;
            std::mt19937 gen(rd());
            std::uniform_int_distribution<> dis(0, 15);
            
            const char* chars = "0123456789abcdef";
            std::string guid;
            
            for (int i = 0; i < 32; ++i) {
                if (i == 8 || i == 12 || i == 16 || i == 20) {
                    guid += '-';
                }
                guid += chars[dis(gen)];
            }
            
            return guid;
        }
        
        void loadFromFile(const std::string& configPath) {
            // Simplified config loading - use defaults for Windows build
            std::cout << "Using default configuration (JSON parsing disabled for Windows build)" << std::endl;
            
            // Check environment variables for bot token (like C# bots)
            const char* envToken = std::getenv("Token");
            if (envToken != nullptr) {
                botToken = std::string(envToken);
                std::cout << "Using bot token from environment variable" << std::endl;
            } else {
                // Generate a new GUID-like token if not found
                botToken = generateGuid();
                std::cout << "Generated new bot token: " << botToken << std::endl;
            }
            
            // Check other environment variables
            const char* envNickname = std::getenv("BOT_NICKNAME");
            if (envNickname != nullptr) {
                botNickname = std::string(envNickname);
            }
            
            const char* envServerUrl = std::getenv("RUNNER_IPV4");
            const char* envServerPort = std::getenv("RUNNER_PORT");
            if (envServerUrl != nullptr && envServerPort != nullptr) {
                serverUrl = "http://" + std::string(envServerUrl) + ":" + std::string(envServerPort);
            }
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
            std::cerr << "Failed to connect to SignalR hub" << std::endl;
            return false;
        }
        
        // Register bot
        if (!signalRClient->registerBot(config.botToken, config.botNickname)) {
            std::cerr << "Failed to register bot" << std::endl;
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
        signalRClient->onGameState([this](const GameState& gameState) {
            handleGameStateUpdate(gameState);
        });
        
        // Registration handler
        signalRClient->onRegistered([this](const std::string& botId) {
            std::cout << "Bot registered with ID: " << botId << std::endl;
        });
        
        // Disconnect handler
        signalRClient->onDisconnect([this](const std::string& reason) {
            std::cout << "Disconnected: " << reason << std::endl;
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
            if (!signalRClient->sendBotCommand(bestAction)) {
                std::cerr << "Failed to send move" << std::endl;
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