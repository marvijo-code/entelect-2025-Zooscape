#include <iostream>
#include <string>
#include "tests/JsonGameStateLoader.h"

using namespace TestUtils;

void printUsage() {
    std::cout << "Usage: GameStateInspector <jsonPath> <botNickname>\n";
}

int main(int argc, char* argv[]) {
    if (argc < 3) {
        printUsage();
        return 1;
    }

    std::string jsonPath = argv[1];
    std::string botNickname = argv[2];

    auto analysisOpt = JsonGameStateLoader::analyzeStateFromFile(jsonPath, botNickname);
    if (!analysisOpt) {
        std::cerr << "Failed to analyze state from file: " << jsonPath << std::endl;
        return 2;
    }

    const StateAnalysis& sa = *analysisOpt;

    std::cout << "Bot Position: (" << sa.myPos.x << ", " << sa.myPos.y << ")\n";
    std::cout << "Score: " << sa.score << "\n";

    auto yesNo = [](bool v) { return v ? "Yes" : "No"; };
    std::cout << "Pellet Up? " << yesNo(sa.pelletUp) << "\n";
    std::cout << "Pellet Left? " << yesNo(sa.pelletLeft) << "\n";
    std::cout << "Pellet Right? " << yesNo(sa.pelletRight) << "\n";
    std::cout << "Pellet Down? " << yesNo(sa.pelletDown) << "\n";

    std::cout << "Pellets Up in 3 steps: " << sa.pelletsUpTo3 << "\n";
    std::cout << "Pellets Left in 3 steps: " << sa.pelletsLeftTo3 << "\n";
    std::cout << "Pellets Right in 3 steps: " << sa.pelletsRightTo3 << "\n";
    std::cout << "Pellets Down in 3 steps: " << sa.pelletsDownTo3 << "\n";

    std::cout << "Consecutive Pellets Up: " << sa.consecutivePelletsUp << "\n";
    std::cout << "Consecutive Pellets Left: " << sa.consecutivePelletsLeft << "\n";
    std::cout << "Consecutive Pellets Right: " << sa.consecutivePelletsRight << "\n";
    std::cout << "Consecutive Pellets Down: " << sa.consecutivePelletsDown << "\n";

    const char* quadNames[4] = {"Top-Left","Top-Right","Bottom-Left","Bottom-Right"};
    for(int q=0;q<4;++q){
        std::cout << "Pellets in " << quadNames[q] << ": " << sa.pelletsPerQuadrant[q] << "\n";
    }
    std::cout << "Current Quadrant: " << quadNames[ sa.currentQuadrant>=0 ? sa.currentQuadrant : 0 ] << "\n";

    if (sa.nearestZookeeperDist != INT_MAX) {
        std::cout << "Nearest Zookeeper: (" << sa.nearestZookeeperPos.x << ", " << sa.nearestZookeeperPos.y << ") at distance " << sa.nearestZookeeperDist << "\n";
    } else {
        std::cout << "No zookeepers present." << std::endl;
    }

    return 0;
}
