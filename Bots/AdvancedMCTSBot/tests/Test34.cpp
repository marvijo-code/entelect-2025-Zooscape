#include <iostream>
#include <fstream>
#include <cassert>
#include <string>
#include "GameState.h"
#include "../MctsService.h"
#include "../Bot.h"
#include "JsonGameStateLoader.h"
#include <iomanip> // For std::fixed and std::setprecision

// Helper to convert action enum to string for logging
std::string actionToString(BotAction action) {
    switch (action) {
        case BotAction::Up: return "Up";
        case BotAction::Down: return "Down";
        case BotAction::Left: return "Left";
        case BotAction::Right: return "Right";
        // Note: 'Stay' is not a standard action in BotAction enum, using 'None' instead.
        case BotAction::None: return "None";
        case BotAction::UseItem: return "UseItem";
        default: return "Unknown";
    }
}

int main() {
    auto gs_optional = TestUtils::JsonGameStateLoader::loadStateFromFile("FunctionalTests/GameStates/34.json", "AdvancedMCTSBot");
    if (!gs_optional) {
        std::cerr << "Failed to load game state from file." << std::endl;
        return 1;
    }
    GameState gs = *gs_optional;

    if (gs.myAnimalId.empty()) {
        std::cerr << "Could not find 'IsMe' in the game state." << std::endl;
        return 1;
    }

    // Instantiate MCTS service with parameters suitable for a functional test
    MctsService mcts(/*maxIterations*/1000000, /*timeLimitMs*/200, /*numThreads*/1, /*maxDepth*/10);
    mcts.SetBotId(gs.myAnimalId);

    MCTSResult result = mcts.GetBestAction(gs);

    std::cout << "--- MCTS Action-Score Breakdown ---" << std::endl;
    for (const auto& action_stat : result.allActionStats) {
        printf("  - Action: %-7s Visits: %7d Avg Score: %.4f\n", 
               actionToString(action_stat.action).c_str(), 
               action_stat.visits, 
               action_stat.avgScore);
    }
    std::cout << "---------------------------------" << std::endl;

    // For this test, we don't have a pre-determined "best" action, 
    // so we just print the result.
    std::cout << "✅ AdvancedMCTSBotTest34 finished – action " << actionToString(result.bestAction) << " selected." << std::endl;

    return 0;
}
