#pragma once

#include "MctsService.h"
#include "GameState.h"
#include "signalrclient/hub_connection.h"
#include <string>
#include <memory>
#include <future>
#include <atomic>
#include <optional>

class Bot {
public:
    Bot();
    void run();
    void requestShutdown();

private:
    struct Config {
        std::string runnerIP = "http://localhost";
        int runnerPort = 5000;
        std::string hubName = "bothub";
        std::string botToken;
        std::string botNickname = "AdvancedMCTSBot";
        int timeLimit = 120; // Reduced from 130 to give more safety margin
        int maxIterations = 50000; // Reduced for faster iterations
    } config;

    void loadConfiguration();

    std::unique_ptr<MctsService> mctsService;
    std::optional<signalr::hub_connection> connection;
    std::promise<void> stop_task;
    std::atomic<int> lastProcessedTick{-1};
};

