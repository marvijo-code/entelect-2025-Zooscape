#include "../include/SignalRClient.h"
#include "../include/GameState.h"
#include <iostream>
#include <chrono>
#include <thread>
#include <sstream>
#include <random>

#ifdef _WIN32
#include <windows.h>
#include <winhttp.h>
#pragma comment(lib, "winhttp.lib")
#endif

// Real HTTP client for Windows using WinHTTP
class WebSocketClient {
private:
    HINTERNET hSession = nullptr;
    HINTERNET hConnect = nullptr;
    HINTERNET hRequest = nullptr;
    std::string serverHost;
    int serverPort;
    bool connected = false;
    std::string botId;
    std::string connectionToken;
    
public:
    WebSocketClient() = default;
    
    ~WebSocketClient() {
        disconnect();
    }
    
    bool connect(const std::string& url) {
        // Parse URL to extract host and port
        // Expected format: http://localhost:5000/bothub
        size_t protocolEnd = url.find("://");
        if (protocolEnd == std::string::npos) return false;
        
        size_t hostStart = protocolEnd + 3;
        size_t portStart = url.find(":", hostStart);
        size_t pathStart = url.find("/", hostStart);
        
        if (portStart != std::string::npos && pathStart != std::string::npos) {
            serverHost = url.substr(hostStart, portStart - hostStart);
            serverPort = std::stoi(url.substr(portStart + 1, pathStart - portStart - 1));
        } else {
            serverHost = "localhost";
            serverPort = 5000;
        }
        
        std::cout << "Attempting to connect to SignalR hub at " << serverHost << ":" << serverPort << std::endl;
        
        // Initialize WinHTTP
        hSession = WinHttpOpen(L"AdvancedMCTSBot/1.0", 
                              WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                              WINHTTP_NO_PROXY_NAME, 
                              WINHTTP_NO_PROXY_BYPASS, 0);
        
        if (!hSession) {
            std::cout << "Failed to initialize WinHTTP session" << std::endl;
            return false;
        }
        
        // Convert host to wide string
        std::wstring wHost(serverHost.begin(), serverHost.end());
        
        hConnect = WinHttpConnect(hSession, wHost.c_str(), serverPort, 0);
        if (!hConnect) {
            std::cout << "Failed to connect to server" << std::endl;
            WinHttpCloseHandle(hSession);
            return false;
        }
        
        // Perform SignalR negotiation
        if (negotiate()) {
            connected = true;
            std::cout << "Successfully connected to SignalR server" << std::endl;
            return true;
        } else {
            std::cout << "SignalR negotiation failed" << std::endl;
            disconnect();
            return false;
        }
    }
    
    bool negotiate() {
        // Step 1: Negotiate connection
        hRequest = WinHttpOpenRequest(hConnect, L"POST", L"/bothub/negotiate",
                                     nullptr, WINHTTP_NO_REFERER, 
                                     WINHTTP_DEFAULT_ACCEPT_TYPES, 0);
        
        if (!hRequest) {
            std::cout << "Failed to create negotiate request" << std::endl;
            return false;
        }
        
        // Add headers for negotiate
        std::wstring headers = L"Content-Type: application/json\r\nContent-Length: 0\r\n";
        WinHttpAddRequestHeaders(hRequest, headers.c_str(), -1, WINHTTP_ADDREQ_FLAG_ADD);
        
        // Send negotiate request
        BOOL result = WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                        WINHTTP_NO_REQUEST_DATA, 0, 0, 0);
        
        if (result) {
            result = WinHttpReceiveResponse(hRequest, nullptr);
        }
        
        if (result) {
            DWORD dwSize = 0;
            DWORD dwDownloaded = 0;
            std::string response;
            
            do {
                dwSize = 0;
                if (!WinHttpQueryDataAvailable(hRequest, &dwSize)) {
                    break;
                }
                
                if (dwSize > 0) {
                    char* pszOutBuffer = new char[dwSize + 1];
                    ZeroMemory(pszOutBuffer, dwSize + 1);
                    
                    if (WinHttpReadData(hRequest, pszOutBuffer, dwSize, &dwDownloaded)) {
                        response += std::string(pszOutBuffer, dwDownloaded);
                    }
                    delete[] pszOutBuffer;
                }
            } while (dwSize > 0);
            
            std::cout << "Negotiate response: " << response << std::endl;
            
            // Parse connection ID from response (SignalR Core uses connectionId, not connectionToken)
            size_t tokenStart = response.find("\"connectionId\":\"");
            if (tokenStart != std::string::npos) {
                tokenStart += 16; // Length of "connectionId":"
                size_t tokenEnd = response.find("\"", tokenStart);
                if (tokenEnd != std::string::npos) {
                    connectionToken = response.substr(tokenStart, tokenEnd - tokenStart);
                    std::cout << "Got connection ID: " << connectionToken.substr(0, 20) << "..." << std::endl;
                }
            }
        }
        
        WinHttpCloseHandle(hRequest);
        hRequest = nullptr;
        
        return result && !connectionToken.empty();
    }
    
    void disconnect() {
        if (hRequest) {
            WinHttpCloseHandle(hRequest);
            hRequest = nullptr;
        }
        if (hConnect) {
            WinHttpCloseHandle(hConnect);
            hConnect = nullptr;
        }
        if (hSession) {
            WinHttpCloseHandle(hSession);
            hSession = nullptr;
        }
        connected = false;
        connectionToken.clear();
        std::cout << "Disconnected from SignalR server" << std::endl;
    }
    
    bool send(const std::string& message) {
        if (!connected || connectionToken.empty()) {
            std::cout << "Not connected to SignalR server" << std::endl;
            return false;
        }
        
        // Create request path with connection ID
        std::wstring requestPath = L"/bothub?id=" + std::wstring(connectionToken.begin(), connectionToken.end()); // connectionToken actually holds the connectionId
        
        hRequest = WinHttpOpenRequest(hConnect, L"POST", requestPath.c_str(),
                                     nullptr, WINHTTP_NO_REFERER, 
                                     WINHTTP_DEFAULT_ACCEPT_TYPES, 0);
        
        if (!hRequest) {
            std::cout << "Failed to create send request" << std::endl;
            return false;
        }
        
        // Add headers
        std::wstring headers = L"Content-Type: application/json\r\n";
        WinHttpAddRequestHeaders(hRequest, headers.c_str(), -1, WINHTTP_ADDREQ_FLAG_ADD);
        
        // Append SignalR record separator and send the request
        std::string messageWithDelimiter = message + '\x1e';
        BOOL bResults = WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                        (LPVOID)messageWithDelimiter.c_str(), messageWithDelimiter.length(), 
                                        messageWithDelimiter.length(), 0);
        
        if (bResults) {
            bResults = WinHttpReceiveResponse(hRequest, nullptr);
            if (bResults) {
                DWORD dwStatusCode = 0;
                DWORD dwSize = sizeof(dwStatusCode);
                WinHttpQueryHeaders(hRequest,
                                    WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                    WINHTTP_HEADER_NAME_BY_INDEX,
                                    &dwStatusCode, &dwSize, WINHTTP_NO_HEADER_INDEX);
                
                // Log the actual path for clarity
                char actualPath[256];
                DWORD actualPathLen = sizeof(actualPath);
                if (WinHttpQueryOption(hRequest, WINHTTP_OPTION_URL_PATH, actualPath, &actualPathLen)) {
                     std::cout << "SignalR POST to " << actualPath << " returned HTTP " << dwStatusCode << std::endl;
                } else {
                     std::cout << "SignalR POST returned HTTP " << dwStatusCode << " (could not get path)" << std::endl;
                }

                if (dwStatusCode >= 200 && dwStatusCode < 300) { // Typically 200 OK, 202 Accepted, or 204 No Content
                    std::cout << "Sent SignalR message: " << message << std::endl;
                    WinHttpCloseHandle(hRequest);
                    hRequest = nullptr;
                    return true;
                } else {
                    std::cerr << "SignalR POST failed with HTTP status " << dwStatusCode << " for message: " << message << std::endl;
                    // You could add code here to read the response body for more detailed error info from the server
                    bResults = FALSE; // Treat non-2xx as a failure for our logic
                }
            } else {
                std::cerr << "WinHttpReceiveResponse failed with WinAPI error: " << GetLastError() << " for message: " << message << std::endl;
            }
        } else {
            std::cerr << "WinHttpSendRequest failed with WinAPI error: " << GetLastError() << " for message: " << message << std::endl;
        }
        
        WinHttpCloseHandle(hRequest);
        hRequest = nullptr;
        
        std::cout << "Failed to send SignalR message (see details above): " << message << std::endl;
        return false;
    }
    
    std::string receive() {
        // For this demo, we'll simulate receiving game state updates
        return "";
    }
    
    bool isConnected() const {
        return connected;
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
    
    std::string fullUrl = serverUrl + "/" + hubName;
    if (wsClient->connect(fullUrl)) {
        isConnected.store(true);
        
        // Start heartbeat thread
        heartbeatThread = std::thread(&SignalRClient::heartbeatLoop, this);
        
        return true;
    }
    
    return false;
}

void SignalRClient::disconnect() {
    if (isConnected.load()) {
        isConnected.store(false);
        
        if (heartbeatThread.joinable()) {
            heartbeatThread.join();
        }
        
        wsClient->disconnect();
    }
}

bool SignalRClient::registerBot(const std::string& token, const std::string& nickname) {
    if (!isConnected.load()) {
        return false;
    }
    
    // Create registration message
    SimpleJson registerMsg;
    registerMsg.addString("method", "Register");
    registerMsg.addString("token", token);
    registerMsg.addString("nickname", nickname);
    
    return wsClient->send(registerMsg.toString());
}

bool SignalRClient::sendBotCommand(BotAction action) {
    if (!isConnected.load()) {
        return false;
    }
    
    // Convert action to string
    std::string actionStr;
    switch (action) {
        case BotAction::Up: actionStr = "UP"; break;
        case BotAction::Down: actionStr = "DOWN"; break;
        case BotAction::Left: actionStr = "LEFT"; break;
        case BotAction::Right: actionStr = "RIGHT"; break;
        case BotAction::UseItem: actionStr = "USE_ITEM"; break;
        default: actionStr = "UP"; break;
    }
    
    SimpleJson commandMsg;
    commandMsg.addString("method", "BotCommand");
    commandMsg.addString("data", actionStr); // Ensure "data" field is a simple string like "UP"
    
    return wsClient->send(commandMsg.toString());
}

void SignalRClient::heartbeatLoop() {
    while (isConnected.load()) {
        if (wsClient->isConnected()) {
            SimpleJson heartbeat;
            heartbeat.addString("method", "Heartbeat");
            heartbeat.addString("data", "ping");
            wsClient->send(heartbeat.toString());
        }
        
        std::this_thread::sleep_for(std::chrono::seconds(30));
    }
}

void SignalRClient::onGameState(std::function<void(const GameState&)> callback) {
    gameStateCallback = callback;
}

void SignalRClient::onRegistered(std::function<void(const std::string&)> callback) {
    registeredCallback = callback;
}

void SignalRClient::onDisconnect(std::function<void(const std::string&)> callback) {
    disconnectCallback = callback;
}

void SignalRClient::processMessage(const std::string& message) {
    // Simple message processing - in a real implementation this would parse JSON
    std::cout << "Received message: " << message << std::endl;
    
    if (message.find("GameState") != std::string::npos && gameStateCallback) {
        // Create a dummy game state for now
        GameState state;
        gameStateCallback(state);
    }
    
    if (message.find("Registered") != std::string::npos && registeredCallback) {
        registeredCallback("bot-id-123");
    }
}
