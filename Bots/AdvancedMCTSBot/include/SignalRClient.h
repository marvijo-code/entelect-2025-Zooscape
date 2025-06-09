#pragma once

#include "GameState.h"
#include <string>
#include <functional>
#include <memory>
#include <thread>
#include <atomic>
#include <mutex>
#include <condition_variable>
#include <queue>
#include <sstream>

// Simplified JSON-like structure for Windows build
struct SimpleJson {
    std::string data;
    
    SimpleJson() = default;
    SimpleJson(const std::string& str) : data(str) {}
    
    std::string asString() const { return data; }
    int asInt() const { return std::stoi(data); }
    double asDouble() const { return std::stod(data); }
    bool asBool() const { return data == "true"; }
};

// Forward declarations
class WebSocketClient;

class SignalRClient {
private:
    // Connection details
    std::string serverUrl;
    std::string hubName;
    std::string connectionToken;
    std::string connectionId;
    
    // Connection state
    std::atomic<bool> isConnected{false};
    std::atomic<bool> shouldStop{false};
    
    // Threading
    std::unique_ptr<std::thread> connectionThread;
    std::unique_ptr<std::thread> heartbeatThread;
    
    // Message handling
    std::queue<std::string> messageQueue;
    std::mutex queueMutex;
    std::condition_variable queueCondition;
    
    // Callbacks
    std::function<void(const GameState&)> onGameStateUpdate;
    std::function<void(const std::string&)> onGameEnd;
    std::function<void(const std::string&)> onError;
    
    // WebSocket client
    std::unique_ptr<WebSocketClient> wsClient;
    
    // Internal methods
    void connectionWorker();
    void heartbeatWorker();
    void processMessage(const std::string& message);
    void handleGameStateMessage(const SimpleJson& data);
    void handleGameEndMessage(const SimpleJson& data);
    void handleErrorMessage(const SimpleJson& data);
    void sendHeartbeat();
    
    // Message parsing
    SimpleJson parseJson(const std::string& jsonStr);
    std::string createJsonMessage(const std::string& method, const SimpleJson& data);
    
    // Connection management
    bool establishConnection();
    void closeConnection();
    
public:
    SignalRClient(const std::string& url, const std::string& hub);
    ~SignalRClient();
    
    // Connection management
    bool connect();
    void disconnect();
    bool isConnectionActive() const;
    
    // Event handlers
    void setGameStateUpdateHandler(std::function<void(const GameState&)> handler);
    void setGameEndHandler(std::function<void(const std::string&)> handler);
    void setErrorHandler(std::function<void(const std::string&)> handler);
    
    // Game actions
    bool sendMove(BotAction action);
    bool joinGame(const std::string& gameId, const std::string& botName);
    bool leaveGame();
    
    // Utility
    std::string getConnectionId() const;
    std::string getLastError() const;
    
private:
    std::string lastError;
    void setError(const std::string& error);
};