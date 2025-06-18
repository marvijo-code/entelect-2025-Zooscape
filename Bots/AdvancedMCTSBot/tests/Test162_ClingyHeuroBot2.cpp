#include "MctsService.h"
#include "GameState.h"
#include "tests/JsonGameStateLoader.h"
#include <iostream>
#include <cassert>
#include <string>
#include <iomanip> // For std::fixed and std::setprecision

// Helper to convert action enum to string for logging
std::string actionToString(BotAction action) {
    switch (action) {
        case BotAction::Up: return "Up";
        case BotAction::Down: return "Down";
        case BotAction::Left: return "Left";
        case BotAction::Right: return "Right";
        case BotAction::UseItem: return "UseItem";
        case BotAction::None: return "None";
        default: return "Unknown";
    }
}

int main()
{
    const std::string jsonPath = "FunctionalTests/GameStates/162.json";
    const std::string botNickname = "ClingyHeuroBot2"; // The bot's name in 162.json

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

    // Instantiate MCTS service with parameters suitable for a functional test
    MctsService mcts(/*maxIterations*/1000000, /*timeLimitMs*/200, /*numThreads*/1, /*maxDepth*/10);
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

    if (result.bestAction == BotAction::Up) {
        std::cout << "✅ AdvancedMCTSBotTest162_ClingyHeuroBot2 passed – action Up selected." << std::endl;
        return 0;
    } else {
        std::cerr << "❌ AdvancedMCTSBotTest162_ClingyHeuroBot2 failed. Expected Up, but got " << actionToString(result.bestAction) << std::endl;
        return 1;
    }
}
