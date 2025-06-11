#define _CRT_SECURE_NO_WARNINGS

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
#include <csignal>
// Removed JSON dependency for Windows build

class AdvancedMCTSBot {
private:
    std::unique_ptr<MCTSEngine> mctsEngine;
    std::unique_ptr<SignalRClient> signalRClient;
    std::atomic<bool> isRunning;
    std::string botId;
    std::atomic<bool> shutdownRequested{false};
    
    // Configuration
    struct BotConfig {
        std::string serverUrl;
        std::string hubName;
        std::string botToken;
        std::string botNickname;
        
        // MCTS Configuration
        double explorationConstant;
        int maxIterations;
        int maxSimulationDepth;
        int timeLimit; // milliseconds
        int numThreads;
        
        // Logging
        bool enableLogging;
        bool enableHeuristicLogging;
        
        BotConfig() : 
            serverUrl("http://localhost:5000"),
            hubName("bothub"),
            botToken(""),
            botNickname("AdvancedMCTSBot"),
            explorationConstant(1.414),
            maxIterations(10000),
            maxSimulationDepth(100),
            timeLimit(1000),
            numThreads(4),
            enableLogging(true),
            enableHeuristicLogging(false)
        {}
        
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
        
        void loadFromFile(const std::string& /*configPath*/) {
            // Simplified config loading - use defaults for Windows build
            std::cout << "Using default configuration (JSON parsing disabled for Windows build)" << std::endl;
            
            // Check environment variables for bot token (like C# bots)
            const char* envToken = std::getenv("Token");
            if (envToken != nullptr) {
                botToken = std::string(envToken);
                std::cout << "Using bot token from environment variable: " << envToken << std::endl;
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
            
            const char* envIp = std::getenv("RUNNER_IPV4");
            const char* envPort = std::getenv("RUNNER_PORT");

            // Use environment variables if available, otherwise keep defaults from constructor
            if (envIp != nullptr && envPort != nullptr) {
                std::string ip = std::string(envIp);
                // Ensure the IP has the http:// scheme, but don't add it if it's already there.
                if (ip.rfind("http://", 0) != 0 && ip.rfind("https://", 0) != 0) {
                    ip = "http://" + ip;
                }
                serverUrl = ip + ":" + std::string(envPort);
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
    void requestShutdown() {
        shutdownRequested.store(true);
    }

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
        
        std::cout << "Waiting for registration confirmation..." << std::endl;
        
        // Wait timeout (30 seconds)
        auto start = std::chrono::steady_clock::now();
        while (botId.empty() && 
               std::chrono::duration_cast<std::chrono::seconds>(std::chrono::steady_clock::now() - start).count() < 30) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        
        if (botId.empty()) {
            std::cerr << "[ERROR] Registration timed out after 30 seconds" << std::endl;
            stop();
            return;
        }
        
        std::cout << "AdvancedMCTSBot is running. Press Ctrl+C to stop." << std::endl;
        
        // Main game loop
        while (isRunning.load() && !shutdownRequested.load()) {
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
        
        // Connection handler
        signalRClient->onConnected([this]() {
            std::cout << "SignalR connection established. Registering bot..." << std::endl;
            if (!signalRClient->registerBot(config.botToken, config.botNickname)) {
                std::cerr << "Failed to send registration request" << std::endl;
            }
        });
        
        // Registration handler
        signalRClient->onRegistered([this](const std::string& receivedBotId) {
            this->botId = receivedBotId;
            std::cout << "[REGISTRATION SUCCESS] Bot registered with ID: " << this->botId << std::endl;
            std::cout << "Animal (AdvancedMCTSBot) added to game world" << std::endl;
            isRunning.store(true);
        });
        
        // Disconnect handler
        signalRClient->onDisconnected([this](const std::string& message) {
            std::cout << "Disconnected from SignalR hub: " << message << std::endl;
            stop();
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
            // Ensure botId is populated before using it
            if (this->botId.empty()) {
                std::cerr << "Bot ID is not set. Cannot determine best action." << std::endl;
                return;
            }
            BotAction bestAction = mctsEngine->findBestAction(gameState, this->botId);
            
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
    if (g_bot) {
        std::cout << "[SIGNAL] Received signal, shutting down..." << signal << std::endl;
        g_bot->requestShutdown();
    }
}

int main(int /*argc*/, char* /*argv*/[]) {
    // Set up signal handling
    std::signal(SIGINT, signalHandler);
    std::signal(SIGTERM, signalHandler);
    
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
