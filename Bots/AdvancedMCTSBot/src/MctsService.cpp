#include "MctsService.h"
#include <thread>

MctsService::MctsService(int maxIterations, int timeLimit) {
    mctsEngine = std::make_unique<MCTSEngine>(
        1.414, // exploration constant
        maxIterations,
        200, // max simulation depth
        timeLimit,
        std::thread::hardware_concurrency()
    );
}

void MctsService::SetBotId(std::string botId) {
    this->botId = botId;
}

BotAction MctsService::GetBestAction(const GameState& gameState) {
    if (botId.empty()) {
        // Return a default/safe action if we don't have an ID yet.
        return BotAction::None;
    }
    return mctsEngine->findBestAction(gameState, botId);
}
