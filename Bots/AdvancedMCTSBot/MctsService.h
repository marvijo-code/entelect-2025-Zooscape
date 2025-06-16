#pragma once

#include "MCTSEngine.h"
#include "GameState.h"
#include <string>
#include <memory>

class MctsService {
public:
    MctsService(int maxIterations, int timeLimit);
    void SetBotId(std::string botId);
    BotAction GetBestAction(const GameState& gameState);

private:
    std::unique_ptr<MCTSEngine> mctsEngine;
    std::string botId;
};
