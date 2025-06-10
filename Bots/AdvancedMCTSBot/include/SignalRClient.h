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
    
    // Add methods needed by implementation
    void addString(const std::string& key, const std::string& value) {
        // Simple key-value format for demo
        data += "\"" + key + "\":\"" + value + "\",";
    }
    
    void addObject(const std::string& key, const SimpleJson& obj) {
        data += "\"" + key + "\":" + obj.toString() + ",";
    }
    
    std::string toString() const {
        std::string temp_data = data;
        if (!temp_data.empty() && temp_data.back() == ',') {
            temp_data.pop_back(); // Remove trailing comma from data if present
        }
        return "{" + temp_data + "}";
    }
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
    std::thread heartbeatThread;
    
    // WebSocket client
    std::unique_ptr<WebSocketClient> wsClient;
    
    // Callbacks
    std::function<void(const GameState&)> gameStateCallback;
    std::function<void(const std::string&)> registeredCallback;
    std::function<void(const std::string&)> disconnectCallback;
    std::function<void()> onConnectedCallback;
    std::function<void(const std::string&)> onDisconnectedCallback;
    
    // Internal methods
    void heartbeatLoop();
    void processMessage(const std::string& message);
    
public:
    SignalRClient(const std::string& url, const std::string& hub);
    ~SignalRClient();
    
    // Connection management
    bool connect();
    void disconnect();
    bool isConnectionActive() const { return isConnected.load(); }
    
    // Bot registration and commands
    bool registerBot(const std::string& token, const std::string& nickname);
    bool sendBotCommand(BotAction action);
    
    // Event handlers
    void onGameState(std::function<void(const GameState&)> callback) {
        gameStateCallback = callback;
    }
    
    void onRegistered(std::function<void(const std::string&)> callback) {
        registeredCallback = callback;
    }
    
    void onDisconnect(std::function<void(const std::string&)> callback) {
        disconnectCallback = callback;
    }
    
    void onConnected(std::function<void()> callback) {
        onConnectedCallback = callback;
    }
    
    void onDisconnected(std::function<void(const std::string&)> callback) {
        onDisconnectedCallback = callback;
    }
    
private:
    std::string lastError;
    void setError(const std::string& error) { lastError = error; }
};
