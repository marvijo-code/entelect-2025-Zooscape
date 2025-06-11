#include "../include/SignalRClient.h"
#include "../include/GameState.h"
#include <iostream>
#include <chrono>
#include <thread>
#include <sstream>
#include <random>
#include <mutex> // Added for std::mutex

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
    HINTERNET hRequest = nullptr; // Per-request handle, managed within send/negotiate
    std::string serverHost;
    int serverPort;
    bool connected = false;
    std::string botId; // This seems unused in WebSocketClient
    std::string connectionToken; // Stores the connectionId from negotiation
    
    static std::mutex consoleOutputMutex; // Mutex for synchronizing console output

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
        // hRequest is closed within send/negotiate, no need to close it here generally
        // if (hRequest) {
        //     WinHttpCloseHandle(hRequest);
        //     hRequest = nullptr;
        // }
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
        std::lock_guard<std::mutex> lock(consoleOutputMutex); // Lock for console output

        if (!connected || connectionToken.empty()) {
            std::cout << "Not connected to SignalR server" << std::endl;
            return false;
        }
        
        // Create request path with connection ID
        std::wstring requestPath = L"/bothub?id=" + std::wstring(connectionToken.begin(), connectionToken.end()); // connectionToken actually holds the connectionId
        
        HINTERNET hSendRequest = WinHttpOpenRequest(hConnect, L"POST", requestPath.c_str(),
                                     nullptr, WINHTTP_NO_REFERER, 
                                     WINHTTP_DEFAULT_ACCEPT_TYPES, 0);
        
        if (!hSendRequest) {
            std::cout << "Failed to create send request" << std::endl;
            return false;
        }
        
        // Add headers
        std::wstring headers = L"Content-Type: application/json\r\n";
        WinHttpAddRequestHeaders(hSendRequest, headers.c_str(), -1, WINHTTP_ADDREQ_FLAG_ADD);
        
        // Ensure exactly **one** SignalR record separator at message end
        std::string messageWithDelimiter = message;
        if (messageWithDelimiter.empty() || messageWithDelimiter.back() != '\x1e') {
            messageWithDelimiter.push_back('\x1e');
        }
        
        BOOL bResults = WinHttpSendRequest(hSendRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
                                        (LPVOID)messageWithDelimiter.c_str(), messageWithDelimiter.length(), 
                                        messageWithDelimiter.length(), 0);
        
        if (bResults) {
            bResults = WinHttpReceiveResponse(hSendRequest, nullptr);
            if (bResults) {
                DWORD dwStatusCode = 0;
                DWORD dwSize = sizeof(dwStatusCode);
                WinHttpQueryHeaders(hSendRequest,
                                    WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                    WINHTTP_HEADER_NAME_BY_INDEX,
                                    &dwStatusCode, &dwSize, WINHTTP_NO_HEADER_INDEX);
                
                // Log the HTTP status
                std::cout << "SignalR POST returned HTTP " << dwStatusCode << std::endl;

                if (dwStatusCode >= 200 && dwStatusCode < 300) {
                    std::cout << "Sent SignalR message: " << message << std::endl;
                    WinHttpCloseHandle(hSendRequest);
                    return true;
                } else {
                    std::cerr << "SignalR POST failed with HTTP status " << dwStatusCode << " for message: " << message << std::endl;
                    bResults = FALSE;
                }
            } else {
                std::cerr << "WinHttpReceiveResponse failed with WinAPI error: " << GetLastError() << " for message: " << message << std::endl;
            }
        } else {
            std::cerr << "WinHttpSendRequest failed with WinAPI error: " << GetLastError() << " for message: " << message << std::endl;
        }
        
        WinHttpCloseHandle(hSendRequest);
        
        std::cout << "Failed to send SignalR message (see details above): " << message << std::endl;
        return false;
    }
    
    std::string receive() {
        // For this demo, we'll simulate receiving game state updates
        // Proper implementation would require a listening mechanism (e.g., on a WebSocket)
        return "";
    }
    
    bool isConnected() const {
        return connected;
    }
};

// Define static member
std::mutex WebSocketClient::consoleOutputMutex;

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
        // Send SignalR handshake: {"protocol":"json","version":1}
        std::string handshake = "{\"protocol\":\"json\",\"version\":1}"; // Record separator appended in send()
        wsClient->send(handshake);

        isConnected.store(true);
        std::cout << "SignalR connection established" << std::endl;
        if (onConnectedCallback) {
            onConnectedCallback();
        }
        return true;
    }
    return false;
}

void SignalRClient::disconnect() {
    if (isConnected.load()) {
        isConnected.store(false);
        wsClient->disconnect();
    }
}

bool SignalRClient::registerBot(const std::string& token, const std::string& nickname) {
    // SignalR invocation format: {"type":1,"target":"Register","arguments":["token","nickname"]}
    std::string message = "{\"type\":1,\"target\":\"Register\",\"arguments\":[\"" + token + "\",\"" + nickname + "\"]}"; // Delimiter added in send()
    
    if (wsClient->send(message)) {
        std::cout << "[REGISTRATION] Sent registration request for bot: " << nickname << std::endl;
        return true;
    }
    return false;
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
    
    // Payload expected by RunnerHub.SendPlayerCommand
    std::string payload = "{\"Action\":\"" + actionStr + "\"}";
    std::string message = "{\"type\":1,\"target\":\"BotCommand\",\"arguments\":[" + payload + "]}\x1e";
    if (wsClient->send(message)) {
        if (static_cast<int>(action) != static_cast<int>(BotAction::Up)) {
            std::cout << "[BOT COMMAND] Sent action " << actionStr << std::endl;
        }
        return true;
    }
    return false;
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
