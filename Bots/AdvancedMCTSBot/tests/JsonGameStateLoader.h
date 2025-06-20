#pragma once

#include "../GameState.h"
#include <string>
#include <optional>

namespace TestUtils {

struct StateAnalysis {
        Position myPos;
        bool pelletUp{false};
        bool pelletLeft{false};
        bool pelletRight{false};
        bool pelletDown{false};
        int pelletsUpTo3{0}; // pellets in 3-tile straight line Up
        int pelletsLeftTo3{0};
        int pelletsRightTo3{0};
        int pelletsDownTo3{0};
        // Count of pellets in consecutive line from the first adjacent pellet (if present)
        int consecutivePelletsUp{0};
        int consecutivePelletsLeft{0};
        int consecutivePelletsRight{0};
        int consecutivePelletsDown{0};
        // Quadrant info (TL=0, TR=1, BL=2, BR=3)
        int pelletsPerQuadrant[4]{0,0,0,0};
        int currentQuadrant{-1};
        int nearestZookeeperDist{INT_MAX};
        Position nearestZookeeperPos{ -1,-1 };
        int score{0};
    };

    class JsonGameStateLoader {
public:
    static std::optional<GameState> loadStateFromFile(const std::string& filePath, const std::string& myBotNickname);
    static StateAnalysis analyzeState(const GameState& gs, const std::string& myBotNickname);
    static std::optional<StateAnalysis> analyzeStateFromFile(const std::string& filePath, const std::string& myBotNickname);
};

}
