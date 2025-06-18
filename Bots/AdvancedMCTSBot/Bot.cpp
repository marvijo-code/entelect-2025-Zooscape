#include "Bot.h"
#include "signalrclient/hub_connection_builder.h"
#include "signalrclient/signalr_value.h"
#include "fmt/core.h"
#include <iostream>
#include <cstdlib>
#include <optional>
#include <string>
#include <random>

namespace {
    // Helper function to safely get environment variables
    std::optional<std::string> getEnvVar(const char* varName) {
        char* buffer = nullptr;
        size_t size = 0;
        if (_dupenv_s(&buffer, &size, varName) == 0 && buffer != nullptr) {
            std::string value(buffer);
            free(buffer);
            return value;
        }
        return std::nullopt;
    }

    GameState convertGameState(const std::vector<signalr::value>& args);
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

    // Helper function to get string representation of signalr::value_type
    std::string get_value_type_string(signalr::value_type type) {
        switch (type) {
            case signalr::value_type::null: return "null";
            case signalr::value_type::boolean: return "boolean";
            case signalr::value_type::float64: return "float64";
            case signalr::value_type::string: return "string";
            case signalr::value_type::array: return "array";
            case signalr::value_type::map: return "map";
            default: return "unknown";
        }
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
        if (map.count(key)) {
            const auto& val = map.at(key);
            if (val.is_null()) {
                fmt::println("DEBUG: Field '{}' is present but null, expected int/double.", key);
            } else if (val.is_double() || val.type() == signalr::value_type::boolean) {
                return static_cast<int>(val.as_double());
            } else {
                fmt::println("DEBUG: Field '{}' is present but has unexpected type '{}', expected int/double.", key, get_value_type_string(val.type()));
            }
        }
        return default_value;
    }

    bool try_get_bool(const std::map<std::string, signalr::value>& map, const std::string& key, bool default_value = false) {
        if (map.count(key)) {
            const auto& val = map.at(key);
            if (val.is_null()) {
                fmt::println("DEBUG: Field '{}' is present but null, expected boolean.", key);
            } else if (val.type() == signalr::value_type::boolean) {
                return val.as_bool();
            } else {
                fmt::println("DEBUG: Field '{}' is present but has unexpected type '{}', expected boolean.", key, get_value_type_string(val.type()));
            }
        }
        return default_value;
    }

    std::string try_get_string(const std::map<std::string, signalr::value>& map, const std::string& key, const std::string& default_value = "") {
        if (map.count(key)) {
            const auto& val = map.at(key);
            if (val.is_null()) {
                fmt::println("DEBUG: Field '{}' is present but null, expected string.", key);
            } else if (val.is_string()) {
                return val.as_string();
            } else {
                fmt::println("DEBUG: Field '{}' is present but has unexpected type '{}', expected string.", key, get_value_type_string(val.type()));
            }
        }
        return default_value;
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
            if (map.count("walls")) state.wallBoard = convertBitBoard(map.at("walls"), state.gridWidth, state.gridHeight);
            if (map.count("pellets")) state.pelletBoard = convertBitBoard(map.at("pellets"), state.gridWidth, state.gridHeight);
            if (map.count("powerPellets")) state.powerUpBoard = convertBitBoard(map.at("powerPellets"), state.gridWidth, state.gridHeight);
        }

        if (map.count("animals") && map.at("animals").is_array()) {
            for (const auto& val : map.at("animals").as_array()) {
                Animal animal = convertAnimal(val);
                if (!animal.id.empty()) {
                    state.animals.push_back(animal);
                }
            }
        }

        if (map.count("zookeepers") && map.at("zookeepers").is_array()) {
            for (const auto& val : map.at("zookeepers").as_array()) {
                Zookeeper zookeeper = convertZookeeper(val);
                if (!zookeeper.id.empty()) {
                    state.zookeepers.push_back(zookeeper);
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
    connection.emplace(signalr::hub_connection_builder::create(hubUrl).build());

    if (connection) {
        connection->on("Registered", [this](const std::vector<signalr::value>& args) {
        if (!args.empty()) {
            std::string botId = args[0].as_string();
            mctsService->SetBotId(botId);
            fmt::println("Bot registered successfully with ID: {}", botId);
        }
    });
    }

    if (connection) {
        connection->on("GameState", [this](const std::vector<signalr::value>& args) {
        try {
            fmt::println("DEBUG: GameState handler invoked.");
            if (!args.empty()) {
                fmt::println("DEBUG: Received args type: {}", get_value_type_string(args[0].type()));
                // Note: Printing full args might be too verbose or complex depending on structure.
                // Consider logging specific parts if needed after seeing the type.
            } else {
                fmt::println("DEBUG: Received empty args for GameState.");
            }

            fmt::println("DEBUG: Attempting to convert game state...");
            GameState gameState = convertGameState(args);
            fmt::println("DEBUG: Game state converted. Tick: {}, Animals: {}, Zookeepers: {}", gameState.tick, gameState.animals.size(), gameState.zookeepers.size());

            fmt::println("DEBUG: Attempting to get best action...");
            MCTSResult result = mctsService->GetBestAction(gameState);
            BotAction command = result.bestAction;
            fmt::println("DEBUG: Best action determined: {}. Responding with action: {}", static_cast<int>(command), static_cast<int>(command));
            
            signalr::value convertedCommand = convertBotAction(command);
            // connection->send("SendPlayerCommand", std::vector<signalr::value>{convertedCommand}, [](std::exception_ptr exc) {
            //     handleExceptionPtr("SendPlayerCommand", exc);
            // });
            fmt::println("DEBUG: SendPlayerCommand was skipped.");
        } catch (const std::exception& e) {
            fmt::println("ERROR in GameState handler: {}", e.what());
            // Optionally, rethrow or handle to ensure stop_task is set if it's a fatal error for the bot's loop
            // For now, just logging to see if this is where the issue originates.
        } catch (...) {
            fmt::println("ERROR in GameState handler: Unknown exception caught.");
        }
    });
    }

    if (connection) {
        connection->on("Disconnect", [this](const std::vector<signalr::value>&) {
        fmt::println("Disconnect message received. Shutting down.");
        stop_task.set_value();
    });
    }

    if (connection) {
        connection->set_disconnected([this](std::exception_ptr exc) {
        fmt::println("Connection disconnected.");
        handleExceptionPtr("Disconnection", exc);
        stop_task.set_value();
    });
    }
}

void Bot::loadConfiguration() {
    if (auto runnerIpEnv = getEnvVar("RUNNER_IPV4_OR_URL")) {
        config.runnerIP = *runnerIpEnv;
    }

    if (auto runnerPortEnv = getEnvVar("RUNNER_PORT")) {
        config.runnerPort = std::stoi(*runnerPortEnv);
    }

    if (auto hubNameEnv = getEnvVar("HUB_NAME")) {
        config.hubName = *hubNameEnv;
    }

    if (auto botNicknameEnv = getEnvVar("BOT_NICKNAME")) {
        config.botNickname = *botNicknameEnv;
    }

    if (auto botTokenEnv = getEnvVar("BOT_TOKEN")) {
        config.botToken = *botTokenEnv;
    } else {
        config.botToken = generateGuid();
    }
    fmt::println("Configuration loaded for bot '{}' connecting to {}:{}/{}", 
        config.botNickname, config.runnerIP, config.runnerPort, config.hubName);
}

void Bot::run() {
    if (!connection) {
        fmt::println("Error: Connection not initialized in Bot::run().");
        return;
    }
    std::promise<void> start_task;
    connection->start([&start_task](std::exception_ptr exc) {
        handleExceptionPtr("Connection Start", exc);
        start_task.set_value();
    });
    start_task.get_future().get();

    std::promise<void> register_task;
    std::vector<signalr::value> registerArgs{config.botToken, config.botNickname};
    connection->send("Register", registerArgs, [&register_task](std::exception_ptr exc) {
        handleExceptionPtr("Registration", exc);
        register_task.set_value();
    });
    register_task.get_future().get();

    fmt::println("Bot is running. Waiting for game to complete...");
    stop_task.get_future().get();

    if (connection) {
        connection->stop([](std::exception_ptr exc) {
        handleExceptionPtr("Connection Stop", exc);
    });
    }
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

