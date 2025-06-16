#pragma once

#include "../GameState.h"
#include <string>
#include <optional>

namespace TestUtils {

class JsonGameStateLoader {
public:
    static std::optional<GameState> loadStateFromFile(const std::string& filePath, const std::string& myBotNickname);
};

}
