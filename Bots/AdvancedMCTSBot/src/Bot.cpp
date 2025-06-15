#include "Bot.h"
#include "signalrclient/hub_connection_builder.h"
#include "signalrclient/signalr_value.h"
#include "fmt/core.h"
#include <iostream>
#include <cstdlib>
#include <random>

namespace {
    std::string generateGuid() {
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

    void handleExceptionPtr(const std::string& context, const std::exception_ptr& exc) {
        if (!exc) return;
        try {
            std::rethrow_exception(exc);
        } catch (const std::exception& e) {
            fmt::println("Error in {}: {}", context, e.what());
        }
    }

    // Helper functions for safe value extraction from signalr::value maps
    int try_get_int(const std::map<std::string, signalr::value>& map, const std::string& key, int default_value = 0) {
        return map.count(key) ? static_cast<int>(map.at(key).as_double()) : default_value;
    }

    bool try_get_bool(const std::map<std::string, signalr::value>& map, const std::string& key, bool default_value = false) {
        return map.count(key) ? map.at(key).as_bool() : default_value;
    }

    std::string try_get_string(const std::map<std::string, signalr::value>& map, const std::string& key, const std::string& default_value = "") {
        return map.count(key) ? map.at(key).as_string() : default_value;
    }

    Position convertPosition(const signalr::value& val) {
        if (!val.is_map()) return {};
        auto map = val.as_map();
        return {try_get_int(map, "x"), try_get_int(map, "y")};
    }

    Animal convertAnimal(const signalr::value& val) {
        if (!val.is_map()) return {};
        auto map = val.as_map();
        Animal animal;
        animal.id = try_get_string(map, "id");
        animal.nickname = try_get_string(map, "nickname");
        animal.position = map.count("position") ? convertPosition(map.at("position")) : Position{};
        animal.spawnPosition = map.count("spawnPosition") ? convertPosition(map.at("spawnPosition")) : Position{};
        animal.score = try_get_int(map, "score");
        animal.capturedCounter = try_get_int(map, "capturedCounter");
        animal.distanceCovered = try_get_int(map, "distanceCovered");
        animal.isViable = try_get_bool(map, "isViable", true);
        animal.heldPowerUp = static_cast<PowerUpType>(try_get_int(map, "heldPowerUp"));
        animal.powerUpDuration = try_get_int(map, "powerUpDuration");
        animal.scoreStreak = try_get_int(map, "scoreStreak", 1);
        animal.ticksSinceLastPellet = try_get_int(map, "ticksSinceLastPellet");
        return animal;
    }

    Zookeeper convertZookeeper(const signalr::value& val) {
        if (!val.is_map()) return {};
        auto map = val.as_map();
        Zookeeper zookeeper;
        zookeeper.id = try_get_string(map, "id");
        zookeeper.position = map.count("position") ? convertPosition(map.at("position")) : Position{};
        zookeeper.targetAnimalId = try_get_string(map, "targetAnimalId");
        zookeeper.ticksSinceTargetUpdate = try_get_int(map, "ticksSinceTargetUpdate");
        return zookeeper;
    }

    BitBoard convertBitBoard(const signalr::value& val, int width, int height) {
        BitBoard board(width, height);
        if (!val.is_array()) return board;
        const auto& rows = val.as_array();
        for (int y = 0; y < rows.size() && y < height; ++y) {
            if (!rows[y].is_array()) continue;
            const auto& cells = rows[y].as_array();
            for (int x = 0; x < cells.size() && x < width; ++x) {
                if (cells[x].as_bool()) {
                    board.set(x, y);
                }
            }
        }
        return board;
    }

    GameState convertGameState(const std::vector<signalr::value>& args) {
        if (args.empty() || !args[0].is_map()) {
            fmt::println("Error: Received invalid bot state format.");
            return {};
        }
        const auto& map = args[0].as_map();

        GameState state;
        state.tick = try_get_int(map, "tick");
        state.gridWidth = try_get_int(map, "gridWidth");
        state.gridHeight = try_get_int(map, "gridHeight");
        state.remainingTicks = try_get_int(map, "remainingTicks");
        state.gameMode = try_get_string(map, "gameMode");

        if (state.gridWidth > 0 && state.gridHeight > 0) {
            if (map.count("walls")) state.walls = convertBitBoard(map.at("walls"), state.gridWidth, state.gridHeight);
            if (map.count("pellets")) state.pellets = convertBitBoard(map.at("pellets"), state.gridWidth, state.gridHeight);
            if (map.count("powerPellets")) state.powerPellets = convertBitBoard(map.at("powerPellets"), state.gridWidth, state.gridHeight);
        }

        if (map.count("animals") && map.at("animals").is_array()) {
            for (const auto& val : map.at("animals").as_array()) {
                Animal animal = convertAnimal(val);
                if (!animal.id.empty()) {
                    state.animals[animal.id] = animal;
                }
            }
        }

        if (map.count("zookeepers") && map.at("zookeepers").is_array()) {
            for (const auto& val : map.at("zookeepers").as_array()) {
                Zookeeper zookeeper = convertZookeeper(val);
                if (!zookeeper.id.empty()) {
                    state.zookeepers[zookeeper.id] = zookeeper;
                }
            }
        }

        return state;
    }

    signalr::value convertBotAction(BotAction action) {
        std::map<std::string, signalr::value> map;
        map.emplace("action", signalr::value(static_cast<double>(action)));
        return signalr::value(map);
    }
}

Bot::Bot() {
    loadConfiguration();
    mctsService = std::make_unique<MctsService>(config.maxIterations, config.timeLimit);

    std::string hubUrl = fmt::format("{}:{}/{}", config.runnerIP, config.runnerPort, config.hubName);
    connection = signalr::hub_connection_builder::create(hubUrl).build();

    connection.on("Registered", [this](const std::vector<signalr::value>& args) {
        if (!args.empty()) {
            std::string botId = args[0].as_string();
            mctsService->SetBotId(botId);
            fmt::println("Bot registered successfully with ID: {}", botId);
        }
    });

    connection.on("ReceiveBotState", [this](const std::vector<signalr::value>& args) {
        fmt::print("Received bot state. ");
        GameState gameState = convertGameState(args);
        BotAction command = mctsService->GetBestAction(gameState);
        fmt::println("Responding with action: {}", static_cast<int>(command));
        
        signalr::value convertedCommand = convertBotAction(command);
        connection.send("SendPlayerCommand", std::vector<signalr::value>{convertedCommand}, [](std::exception_ptr exc) {
            handleExceptionPtr("SendPlayerCommand", exc);
        });
    });

    connection.on("Disconnect", [this](const std::vector<signalr::value>&) {
        fmt::println("Disconnect message received. Shutting down.");
        stop_task.set_value();
    });

    connection.set_disconnected([this](std::exception_ptr exc) {
        fmt::println("Connection disconnected.");
        handleExceptionPtr("Disconnection", exc);
        stop_task.set_value();
    });
}

void Bot::loadConfiguration() {
    const char* runnerIpEnv = std::getenv("RUNNER_IPV4_OR_URL");
    if (runnerIpEnv) config.runnerIP = runnerIpEnv;

    const char* runnerPortEnv = std::getenv("RUNNER_PORT");
    if (runnerPortEnv) config.runnerPort = std::stoi(runnerPortEnv);

    const char* hubNameEnv = std::getenv("HUB_NAME");
    if (hubNameEnv) config.hubName = hubNameEnv;

    const char* botNicknameEnv = std::getenv("BOT_NICKNAME");
    if (botNicknameEnv) config.botNickname = botNicknameEnv;

    const char* botTokenEnv = std::getenv("BOT_TOKEN");
    if (botTokenEnv) {
        config.botToken = botTokenEnv;
    } else {
        config.botToken = generateGuid();
    }
    fmt::println("Configuration loaded for bot '{}' connecting to {}:{}/{}", 
        config.botNickname, config.runnerIP, config.runnerPort, config.hubName);
}

void Bot::run() {
    std::promise<void> start_task;
    connection.start([&start_task](std::exception_ptr exc) {
        handleExceptionPtr("Connection Start", exc);
        start_task.set_value();
    });
    start_task.get_future().get();

    std::promise<void> register_task;
    std::vector<signalr::value> registerArgs{config.botToken, config.botNickname};
    connection.send("Register", registerArgs, [&register_task](std::exception_ptr exc) {
        handleExceptionPtr("Registration", exc);
        register_task.set_value();
    });
    register_task.get_future().get();

    fmt::println("Bot is running. Waiting for game to complete...");
    stop_task.get_future().get();

    connection.stop([](std::exception_ptr exc) {
        handleExceptionPtr("Connection Stop", exc);
    });
}

void Bot::requestShutdown() {
    fmt::println("Shutdown requested via signal.");
    // This will unblock the main thread in run()
    try {
        stop_task.set_value();
    } catch (const std::future_error& e) {
        // Ignore cases where the promise is already set (e.g., disconnected and then Ctrl+C)
        fmt::println("Shutdown already in progress: {}", e.what());
    }
}

