#include "Bot.h"
#include "signalrclient/hub_connection_builder.h"
#include "signalrclient/signalr_value.h"
#include "fmt/core.h"
#include <objbase.h>
#include <thread>
#include <chrono>
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

    Animal convertAnimal(const signalr::value& val) {
        if (!val.is_map()) return {};
        auto map = val.as_map();
        Animal animal;
        animal.id = try_get_string(map, "id");
        animal.nickname = try_get_string(map, "nickname");
        animal.position = {try_get_int(map, "x"), try_get_int(map, "y")};
        animal.spawnPosition = {try_get_int(map, "spawnX"), try_get_int(map, "spawnY")};
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
        zookeeper.position = {try_get_int(map, "x"), try_get_int(map, "y")};
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
        state.remainingTicks = try_get_int(map, "remainingTicks");
        state.gameMode = try_get_string(map, "gameMode");

        if (map.count("cells") && map.at("cells").is_array()) {
            const auto& cells = map.at("cells").as_array();

            // First pass: determine grid dimensions from the sparse cell list
            int max_x = 0;
            int max_y = 0;
            for (const auto& cell_val : cells) {
                if (!cell_val.is_map()) continue;
                const auto& cell_map = cell_val.as_map();
                int x = try_get_int(cell_map, "x", -1);
                int y = try_get_int(cell_map, "y", -1);
                if (x > max_x) max_x = x;
                if (y > max_y) max_y = y;
            }

            int gridWidth = max_x + 1;
            int gridHeight = max_y + 1;

            // Initialize the grid and bitboards in the GameState object
            if (gridWidth > 0 && gridHeight > 0) {
                state.initializeGrid(gridWidth, gridHeight);

                // Second pass: populate the grid using the GameState's setCell method
                for (const auto& cell_val : cells) {
                    if (!cell_val.is_map()) continue;
                    const auto& cell_map = cell_val.as_map();

                    int x = try_get_int(cell_map, "x", -1);
                    int y = try_get_int(cell_map, "y", -1);
                    if (x != -1 && y != -1) {
                        int content_val = try_get_int(cell_map, "content", 0); // Default to Empty
                        state.setCell(x, y, static_cast<CellContent>(content_val));
                    }
                }
            }
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


} // End of anonymous namespace

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
        BotAction chosenActionType = BotAction::None; // Default/fallback action

        try {
            GameState gameState = convertGameState(args);
            MCTSResult mctsResult = mctsService->GetBestAction(gameState);
            chosenActionType = mctsResult.bestAction;

        } catch (const std::exception& e) {
            fmt::println("ERROR during MCTS calculation: {}. Sending default action.", e.what());
        } catch (...) {
            fmt::println("ERROR during MCTS calculation: Unknown exception. Sending default action.");
        }

        // Always send a command to ensure the bot acts every tick
        BotActionCommand commandToSend;
        commandToSend.actionType = chosenActionType;
        commandToSend.targetX = 0;
        commandToSend.targetY = 0;

        std::map<std::string, signalr::value> commandMap;
        commandMap["Action"] = signalr::value(static_cast<double>(commandToSend.actionType));

        connection->send("BotCommand", std::vector<signalr::value>{commandMap}, [](std::exception_ptr exc) {
            handleExceptionPtr("BotCommand", exc);
        });
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
    auto runnerIpEnv = getEnvVar("RUNNER_IPV4_OR_URL");
    auto runnerPortEnv = getEnvVar("RUNNER_PORT");
    auto hubNameEnv = getEnvVar("HUB_NAME");
    auto botNicknameEnv = getEnvVar("BOT_NICKNAME");

    if (!runnerIpEnv || runnerIpEnv->empty()) {
        runnerIpEnv = "http://localhost";
    }
    config.runnerIP = *runnerIpEnv;

    if (!runnerPortEnv || runnerPortEnv->empty()) {
        runnerPortEnv = "5000";
    }
    config.runnerPort = std::stoi(*runnerPortEnv);

    if (!hubNameEnv || hubNameEnv->empty()) {
        hubNameEnv = "bothub";
    }
    config.hubName = *hubNameEnv;

    if (!botNicknameEnv || botNicknameEnv->empty()) {
        botNicknameEnv = "AdvancedMCTSBot";
    }
    config.botNickname = *botNicknameEnv;

    if (auto botTokenEnv = getEnvVar("Token")) {
        config.botToken = *botTokenEnv;
    } else {
        config.botToken = generateGuid();
        fmt::println("Info: Token not set, generated a new GUID: {}", config.botToken);
    }

    if (auto timeLimitEnv = getEnvVar("MCTS_TIME_LIMIT_MS")) {
        try {
            config.timeLimit = std::stoi(*timeLimitEnv);
            fmt::println("Info: MCTS_TIME_LIMIT_MS environment variable set to: {}", config.timeLimit);
        } catch (const std::invalid_argument& e) {
            fmt::println("Warning: Invalid MCTS_TIME_LIMIT_MS value '{}'. Using default {}.", *timeLimitEnv, config.timeLimit);
        } catch (const std::out_of_range& e) {
            fmt::println("Warning: MCTS_TIME_LIMIT_MS value '{}' is out of range. Using default {}.", *timeLimitEnv, config.timeLimit);
        }
    }

    fmt::println("Configuration loaded for bot '{}' connecting to {}:{}/{}", 
        config.botNickname, config.runnerIP, config.runnerPort, config.hubName);
}

void Bot::run() {
    if (!connection) {
        fmt::println("Error: Connection not initialized in Bot::run().");
        return;
    }

    bool connected = false;
    const int max_retries = 5;
    const auto retry_delay = std::chrono::seconds(5);

    for (int i = 0; i < max_retries; ++i) {
        fmt::println("Attempting to connect (Attempt {}/{})", i + 1, max_retries);
        std::promise<bool> start_promise;
        connection->start([&start_promise](std::exception_ptr exc) {
            handleExceptionPtr("Connection Start", exc);
            start_promise.set_value(!exc); // true if exc is null (success), false otherwise
        });

        if (start_promise.get_future().get()) {
            connected = true;
            fmt::println("Connection successful.");
            break;
        }

        if (i < max_retries - 1) {
            fmt::println("Connection failed. Retrying in {} seconds...", retry_delay.count());
            std::this_thread::sleep_for(retry_delay);
        }
    }

    if (!connected) {
        fmt::println("FATAL: Could not connect to the server after {} attempts. Shutting down.", max_retries);
        return;
    }

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

