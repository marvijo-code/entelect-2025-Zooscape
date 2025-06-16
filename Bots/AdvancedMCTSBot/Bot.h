#pragma once

#include "MctsService.h"
#include "GameState.h"
#include "signalrclient/hub_connection.h"
#include <string>
#include <memory>
#include <future>

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
        int timeLimit = 950;
        int maxIterations = 10000;
    } config;

    void loadConfiguration();

    std::unique_ptr<MctsService> mctsService;
    signalr::hub_connection connection;
    std::promise<void> stop_task;
};

