#pragma once
#include "MctsService.h"
#include "GameState.h"
#include "tests/JsonGameStateLoader.h"
#include <iomanip>
#include <iostream>
#include <string>

inline std::string actionToString(BotAction action) {
    switch (action) {
        case BotAction::Up:      return "Up";
        case BotAction::Down:    return "Down";
        case BotAction::Left:    return "Left";
        case BotAction::Right:   return "Right";
        case BotAction::UseItem: return "UseItem";
        case BotAction::None:    return "None";
        default:                 return "Unknown";
    }
}

inline int runScenario(const std::string& jsonPath,
                       const std::string& botNickname,
                       BotAction expectedAction,
                       int timeLimitMs = 200,
                       int maxIterations = 1'000'000,
                       int maxDepth = 10)
{
    auto gameStateOpt = TestUtils::JsonGameStateLoader::loadStateFromFile(jsonPath, botNickname);
    if (!gameStateOpt) {
        std::cerr << "Test failed: Could not load game state from " << jsonPath << std::endl;
        return 1;
    }

    GameState& gs = *gameStateOpt;
    if (gs.myAnimalId.empty()) {
        std::cerr << "Test failed: Bot '" << botNickname << "' not found in the game state." << std::endl;
        return 1;
    }

    MctsService mcts(maxIterations, timeLimitMs, /*numThreads*/1, maxDepth);
    mcts.SetBotId(gs.myAnimalId);
    MCTSResult result = mcts.GetBestAction(gs);

    std::cout << "--- MCTS Action-Score Breakdown --- " << botNickname << " ---" << std::endl;
    std::cout << std::fixed << std::setprecision(4);
    for (const auto& stats : result.allActionStats) {
        std::cout << "  - Action: " << std::setw(8) << std::left << actionToString(stats.action)
                  << " Visits: " << std::setw(6) << std::right << stats.visits
                  << " Avg Score: " << stats.avgScore << std::endl;
    }
    std::cout << "---------------------------------" << std::endl;

    if (result.bestAction == expectedAction) {
        std::cout << "✅ Scenario passed – expected action selected." << std::endl;
        return 0;
    } else {
        std::cerr << "❌ Scenario failed. Expected " << actionToString(expectedAction)
                  << ", but got " << actionToString(result.bestAction) << std::endl;
        return 1;
    }
}
