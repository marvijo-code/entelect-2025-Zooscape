#pragma once

#include "MCTSEngine.h"
#include "GameState.h"
#include <string>
#include <memory>

class MctsService {
public:
    MctsService(int maxIterations, int timeLimit, int numThreads = 0, int maxDepth = 200);
    void SetBotId(std::string botId);
    MCTSResult GetBestAction(const GameState& gameState);

private:
    std::unique_ptr<MCTSEngine> mctsEngine;
    std::string botId;
};
