#include "tests/CommonFunctionalTest.h"

int main() {
    return runScenario("FunctionalTests/GameStates/805.json", "AdvancedMCTSBot", BotAction::Up, 200);
}
