#include "MctsService.h"
#include "GameState.h"
#include "tests/JsonGameStateLoader.h"
#include "tests/CommonFunctionalTest.h"
#include <iostream>
#include <cassert>
#include <string>
#include <iomanip>
#include <vector>

// actionToString is already defined in CommonFunctionalTest.h

// Test results structure
struct TestResult {
    std::string testName;
    bool passed;
    std::string message;
};

// Test 1: Cycle Detection Test
TestResult runCycleDetectionTest() {
    std::cout << "\n=== Running Cycle Detection Test ===" << std::endl;
    
    // Create a simple test scenario - just a straight path to a pellet
    GameState gs(7, 7);
    
    // Set up walls around the perimeter
    for (int x = 0; x < 7; x++) {
        gs.setCell(x, 0, CellContent::Wall);  // Top wall
        gs.setCell(x, 6, CellContent::Wall);  // Bottom wall
    }
    for (int y = 0; y < 7; y++) {
        gs.setCell(0, y, CellContent::Wall);  // Left wall
        gs.setCell(6, y, CellContent::Wall);  // Right wall
    }
    
    // Place a pellet at the center
    gs.setCell(3, 3, CellContent::Pellet);
    
    // Create animal at starting position
    Animal animal;
    animal.id = "testBot";
    animal.position = Position(1, 1);  // Start at top-left corner
    animal.score = 0;
    animal.scoreStreak = 0;
    animal.ticksSinceLastPellet = 0;
    animal.isCaught = false;
    animal.heldPowerUp = PowerUpType::None;
    animal.powerUpDuration = 0;
    
    gs.animals.push_back(animal);
    gs.myAnimalId = "testBot";
    gs.tick = 1;
    gs.remainingTicks = 100;
    
    // Initialize MCTS service
    MctsService mcts(/*maxIterations*/10000, /*timeLimitMs*/500, /*numThreads*/1, /*maxDepth*/30);
    mcts.SetBotId(gs.myAnimalId);
    
    // Test multiple iterations to ensure bot doesn't get stuck
    std::vector<BotAction> actionSequence;
    int maxSteps = 20;
    bool foundPellet = false;
    
    for (int step = 0; step < maxSteps; step++) {
        MCTSResult result = mcts.GetBestAction(gs);
        
        std::cout << "Step " << step + 1 << ": Action = " << actionToString(result.bestAction) << std::endl;
        
        // Apply action to game state
        gs.applyAction(gs.myAnimalId, result.bestAction);
        actionSequence.push_back(result.bestAction);
        
        // Check if we found the pellet
        Animal* animal = gs.getAnimal(gs.myAnimalId);
        if (animal && animal->score > 0) {
            foundPellet = true;
            std::cout << "✅ Pellet found at step " << step + 1 << "!" << std::endl;
            break;
        }
        
        // Check for actual harmful cycles (not just repeated directions)
        if (actionSequence.size() >= 4) {
            // Check if we've returned to a previous position within the last few moves
            Position currentPos = animal->position;
            bool foundCycle = false;
            
            // Look back through recent positions (simulate the moves)
            Position testPos = currentPos;
            for (int lookback = 1; lookback <= std::min(6, (int)actionSequence.size()); lookback++) {
                // Reverse the action to get previous position
                BotAction reverseAction = actionSequence[actionSequence.size() - lookback];
                switch (reverseAction) {
                    case BotAction::Up: testPos.y++; break;
                    case BotAction::Down: testPos.y--; break;
                    case BotAction::Left: testPos.x++; break;
                    case BotAction::Right: testPos.x--; break;
                    default: break;
                }
                
                // If we've been at this position before recently, it might be a cycle
                if (lookback >= 4 && testPos == currentPos) {
                    foundCycle = true;
                    break;
                }
            }
            
            if (foundCycle) {
                return {"CycleDetection", false, "Position cycle detected - returning to same location!"};
            }
        }
        
        // Check for simple back-and-forth cycles
        if (actionSequence.size() >= 6) {
            bool isBackAndForth = true;
            for (int i = 0; i < 3; i++) {
                BotAction action1 = actionSequence[actionSequence.size() - 1 - (i * 2)];
                BotAction action2 = actionSequence[actionSequence.size() - 2 - (i * 2)];
                
                // Check if actions are opposites
                bool areOpposites = (action1 == BotAction::Up && action2 == BotAction::Down) ||
                                   (action1 == BotAction::Down && action2 == BotAction::Up) ||
                                   (action1 == BotAction::Left && action2 == BotAction::Right) ||
                                   (action1 == BotAction::Right && action2 == BotAction::Left);
                
                if (!areOpposites) {
                    isBackAndForth = false;
                    break;
                }
            }
            if (isBackAndForth) {
                return {"CycleDetection", false, "Back-and-forth cycle detected!"};
            }
        }
        
        gs.tick++;
    }
    
    if (foundPellet) {
        return {"CycleDetection", true, "Bot successfully navigated maze without cycles!"};
    } else {
        return {"CycleDetection", false, "Bot did not find pellet within " + std::to_string(maxSteps) + " steps"};
    }
}

// Test 2: Test162 (existing functional test)
TestResult runTest162() {
    std::cout << "\n=== Running Test162 ===" << std::endl;
    
    const std::string jsonPath = "../../../../FunctionalTests/GameStates/162.json";
    const std::string botNickname = "MarvijoClingyBot";
    
    auto gameStateOpt = TestUtils::JsonGameStateLoader::loadStateFromFile(jsonPath, botNickname);
    if (!gameStateOpt) {
        return {"Test162", false, "Could not load game state from " + jsonPath};
    }
    
    GameState& gs = *gameStateOpt;
    if (gs.myAnimalId.empty()) {
        return {"Test162", false, "Bot '" + botNickname + "' not found in the game state."};
    }
    
    MctsService mcts(/*maxIterations*/1000000, /*timeLimitMs*/950, /*numThreads*/1, /*maxDepth*/20);
    mcts.SetBotId(gs.myAnimalId);
    
    MCTSResult result = mcts.GetBestAction(gs);
    
    std::cout << "--- MCTS Action-Score Breakdown ---" << std::endl;
    std::cout << std::fixed << std::setprecision(4);
    for (const auto& stats : result.allActionStats) {
        std::cout << "  - Action: " << std::setw(8) << std::left << actionToString(stats.action)
                  << " Visits: " << std::setw(6) << std::right << stats.visits
                  << " Avg Score: " << stats.avgScore << std::endl;
    }
    std::cout << "---------------------------------" << std::endl;
    
    if (result.bestAction == BotAction::Right) {
        return {"Test162", true, "Action Right selected as expected"};
    } else {
        return {"Test162", false, "Expected Right, but got " + actionToString(result.bestAction)};
    }
}

// Test 3: Test34 (existing functional test)
TestResult runTest34() {
    std::cout << "\n=== Running Test34 ===" << std::endl;
    
    const std::string jsonPath = "../../../../FunctionalTests/GameStates/34.json";
    const std::string botNickname = "MarvijoClingyBot";
    
    auto gameStateOpt = TestUtils::JsonGameStateLoader::loadStateFromFile(jsonPath, botNickname);
    if (!gameStateOpt) {
        return {"Test34", false, "Could not load game state from " + jsonPath};
    }
    
    GameState& gs = *gameStateOpt;
    if (gs.myAnimalId.empty()) {
        return {"Test34", false, "Bot '" + botNickname + "' not found in the game state."};
    }
    
    MctsService mcts(/*maxIterations*/1000000, /*timeLimitMs*/950, /*numThreads*/1, /*maxDepth*/20);
    mcts.SetBotId(gs.myAnimalId);
    
    MCTSResult result = mcts.GetBestAction(gs);
    
    std::cout << "Best action: " << actionToString(result.bestAction) << std::endl;
    
    if (result.bestAction == BotAction::Down) {
        return {"Test34", true, "Action Down selected as expected"};
    } else {
        return {"Test34", false, "Expected Down, but got " + actionToString(result.bestAction)};
    }
}

// Test 4: Test805 (existing functional test)
TestResult runTest805() {
    std::cout << "\n=== Running Test805 ===" << std::endl;
    
    const std::string jsonPath = "../../../../FunctionalTests/GameStates/805.json";
    const std::string botNickname = "AdvancedMCTSBot";
    
    auto gameStateOpt = TestUtils::JsonGameStateLoader::loadStateFromFile(jsonPath, botNickname);
    if (!gameStateOpt) {
        return {"Test805", false, "Could not load game state from " + jsonPath};
    }
    
    GameState& gs = *gameStateOpt;
    if (gs.myAnimalId.empty()) {
        return {"Test805", false, "Bot '" + botNickname + "' not found in the game state."};
    }
    
    MctsService mcts(/*maxIterations*/1000000, /*timeLimitMs*/950, /*numThreads*/1, /*maxDepth*/20);
    mcts.SetBotId(gs.myAnimalId);
    
    MCTSResult result = mcts.GetBestAction(gs);
    
    std::cout << "Best action: " << actionToString(result.bestAction) << std::endl;
    
    if (result.bestAction == BotAction::Up) {
        return {"Test805", true, "Action Up selected as expected"};
    } else {
        return {"Test805", false, "Expected Up, but got " + actionToString(result.bestAction)};
    }
}

int main() {
    std::cout << "=== AdvancedMCTSBot Comprehensive Test Suite ===" << std::endl;
    
    std::vector<TestResult> results;
    
    // Run all tests
    results.push_back(runCycleDetectionTest());
    results.push_back(runTest162());
    results.push_back(runTest34());
    results.push_back(runTest805());
    
    // Print summary
    std::cout << "\n=== Test Results Summary ===" << std::endl;
    int passed = 0;
    int total = static_cast<int>(results.size());
    
    for (const auto& result : results) {
        std::string status = result.passed ? "✅ PASS" : "❌ FAIL";
        std::cout << status << " " << result.testName << ": " << result.message << std::endl;
        if (result.passed) passed++;
    }
    
    std::cout << "\n" << passed << "/" << total << " tests passed." << std::endl;
    
    return (passed == total) ? 0 : 1;
} 