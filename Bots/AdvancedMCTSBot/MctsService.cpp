#include "MctsService.h"
#include <thread>

MctsService::MctsService(int maxIterations, int timeLimit, int numThreads, int maxDepth) {
    unsigned int finalNumThreads = (numThreads == 0) ? std::thread::hardware_concurrency() : numThreads;
    if (finalNumThreads == 0) {
        finalNumThreads = 1; // hardware_concurrency() can return 0, ensure at least 1 thread
    }

    mctsEngine = std::make_unique<MCTSEngine>(
        1.414, // exploration constant
        maxIterations,
        maxDepth,
        timeLimit,
        finalNumThreads
    );
}

void MctsService::SetBotId(std::string botId) {
    this->botId = botId;
}

MCTSResult MctsService::GetBestAction(const GameState& gameState) {
    if (botId.empty()) {
        // Return a default/safe action if we don't have an ID yet.
        return MCTSResult{BotAction::None, {}};
    }
    return mctsEngine->findBestAction(gameState, botId);
}
