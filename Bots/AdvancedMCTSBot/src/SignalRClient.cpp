#include "../include/SignalRClient.h"
#include "../include/GameState.h"
#include <iostream>
#include <chrono>
#include <thread>

// Simplified WebSocket client for Windows build
class WebSocketClient {
public:
    WebSocketClient() = default;
    ~WebSocketClient() = default;
    
    bool connect(const std::string& url) {
        // Simplified connection - just simulate success for now
        std::cout << "Simulating WebSocket connection to: " << url << std::endl;
        return true;
    }
    
    void disconnect() {
        std::cout << "Simulating WebSocket disconnect" << std::endl;
    }
    
    bool send(const std::string& message) {
        std::cout << "Simulating WebSocket send: " << message << std::endl;
        return true;
    }
    
    std::string receive() {
        // Simulate receiving messages
        return "";
    }
    
    bool isConnected() const {
        return true; // Simplified
    }
};

SignalRClient::SignalRClient(const std::string& url, const std::string& hub)
    : serverUrl(url), hubName(hub), wsClient(std::make_unique<WebSocketClient>()) {
}

SignalRClient::~SignalRClient() {
    disconnect();
}

bool SignalRClient::connect() {
    if (isConnected.load()) {
        return true;
    }
    
    try {
        if (!establishConnection()) {
            setError("Failed to establish connection");
            return false;
        }
        
        isConnected.store(true);
        shouldStop.store(false);
        
        // Start worker threads
        connectionThread = std::make_unique<std::thread>(&SignalRClient::connectionWorker, this);
        heartbeatThread = std::make_unique<std::thread>(&SignalRClient::heartbeatWorker, this);
        
        std::cout << "SignalR connection established" << std::endl;
        return true;
    }
    catch (const std::exception& e) {
        setError("Connection failed: " + std::string(e.what()));
        return false;
    }
}

void SignalRClient::disconnect() {
    if (!isConnected.load()) {
        return;
    }
    
    shouldStop.store(true);
    isConnected.store(false);
    
    // Wake up waiting threads
    queueCondition.notify_all();
    
    // Join threads
    if (connectionThread && connectionThread->joinable()) {
        connectionThread->join();
    }
    if (heartbeatThread && heartbeatThread->joinable()) {
        heartbeatThread->join();
    }
    
    closeConnection();
    std::cout << "SignalR connection closed" << std::endl;
}

bool SignalRClient::isConnectionActive() const {
    return isConnected.load();
}

void SignalRClient::setGameStateUpdateHandler(std::function<void(const GameState&)> handler) {
    onGameStateUpdate = handler;
}

void SignalRClient::setGameEndHandler(std::function<void(const std::string&)> handler) {
    onGameEnd = handler;
}

void SignalRClient::setErrorHandler(std::function<void(const std::string&)> handler) {
    onError = handler;
}

bool SignalRClient::sendMove(BotAction action) {
    if (!isConnected.load()) {
        setError("Not connected");
        return false;
    }
    
    try {
        // Convert action to string representation
        std::string actionStr;
        switch (action) {
            case BotAction::Up: actionStr = "UP"; break;
            case BotAction::Down: actionStr = "DOWN"; break;
            case BotAction::Left: actionStr = "LEFT"; break;
            case BotAction::Right: actionStr = "RIGHT"; break;
            case BotAction::UseItem: actionStr = "USE_ITEM"; break;`
            default: actionStr = "UP"; break;
        }
        
        SimpleJson data(actionStr);
        std::string message = createJsonMessage("SendMove", data);
        
        return wsClient->send(message);
    }
    catch (const std::exception& e) {
        setError("Failed to send move: " + std::string(e.what()));
        return false;
    }
}

bool SignalRClient::joinGame(const std::string& gameId, const std::string& botName) {
    if (!isConnected.load()) {
        setError("Not connected");
        return false;
    }
    
    try {
        SimpleJson data(gameId + ":" + botName);
        std::string message = createJsonMessage("JoinGame", data);
        
        return wsClient->send(message);
    }
    catch (const std::exception& e) {
        setError("Failed to join game: " + std::string(e.what()));
        return false;
    }
}

bool SignalRClient::leaveGame() {
    if (!isConnected.load()) {
        return true;
    }
    
    try {
        SimpleJson data("");
        std::string message = createJsonMessage("LeaveGame", data);
        
        return wsClient->send(message);
    }
    catch (const std::exception& e) {
        setError("Failed to leave game: " + std::string(e.what()));
        return false;
    }
}

std::string SignalRClient::getConnectionId() const {
    return connectionId;
}

std::string SignalRClient::getLastError() const {
    return lastError;
}

void SignalRClient::connectionWorker() {
    while (!shouldStop.load()) {
        try {
            std::string message = wsClient->receive();
            if (!message.empty()) {
                processMessage(message);
            }
            
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        catch (const std::exception& e) {
            setError("Connection worker error: " + std::string(e.what()));
            break;
        }
    }
}

void SignalRClient::heartbeatWorker() {
    while (!shouldStop.load()) {
        try {
            sendHeartbeat();
            std::this_thread::sleep_for(std::chrono::seconds(30));
        }
        catch (const std::exception& e) {
            setError("Heartbeat error: " + std::string(e.what()));
            break;
        }
    }
}

void SignalRClient::processMessage(const std::string& message) {
    try {
        SimpleJson json = parseJson(message);
        
        // Simple message type detection based on content
        if (message.find("GameState") != std::string::npos) {
            handleGameStateMessage(json);
        }
        else if (message.find("GameEnd") != std::string::npos) {
            handleGameEndMessage(json);
        }
        else if (message.find("Error") != std::string::npos) {
            handleErrorMessage(json);
        }
    }
    catch (const std::exception& e) {
        setError("Failed to process message: " + std::string(e.what()));
    }
}

void SignalRClient::handleGameStateMessage(const SimpleJson& data) {
    if (onGameStateUpdate) {
        // Create a dummy game state for now
        GameState state;
        onGameStateUpdate(state);
    }
}

void SignalRClient::handleGameEndMessage(const SimpleJson& data) {
    if (onGameEnd) {
        onGameEnd(data.asString());
    }
}

void SignalRClient::handleErrorMessage(const SimpleJson& data) {
    if (onError) {
        onError(data.asString());
    }
}

void SignalRClient::sendHeartbeat() {
    if (isConnected.load()) {
        SimpleJson data("ping");
        std::string message = createJsonMessage("Heartbeat", data);
        wsClient->send(message);
    }
}

SimpleJson SignalRClient::parseJson(const std::string& jsonStr) {
    // Simplified JSON parsing - just return the string
    return SimpleJson(jsonStr);
}

std::string SignalRClient::createJsonMessage(const std::string& method, const SimpleJson& data) {
    // Simplified JSON creation
    return "{\"method\":\"" + method + "\",\"data\":\"" + data.asString() + "\"}";
}

bool SignalRClient::establishConnection() {
    connectionId = "test-connection-id";
    connectionToken = "test-token";
    
    return wsClient->connect(serverUrl + "/" + hubName);
}

void SignalRClient::closeConnection() {
    if (wsClient) {
        wsClient->disconnect();
    }
}

void SignalRClient::setError(const std::string& error) {
    lastError = error;
    std::cerr << "SignalR Error: " << error << std::endl;
}