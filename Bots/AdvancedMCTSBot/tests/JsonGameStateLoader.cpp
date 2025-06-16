#include "JsonGameStateLoader.h"
#include <nlohmann/json.hpp>
#include <fstream>
#include <iostream>

using json = nlohmann::json;

namespace TestUtils {

// Helper to safely get a value from a json object or a default value
template<typename T>
T get_optional_value(const json& j, const std::string& key, T default_value) {
    return j.contains(key) ? j.at(key).get<T>() : default_value;
}

std::optional<GameState> JsonGameStateLoader::loadStateFromFile(const std::string& filePath, const std::string& myBotNickname) {
    std::ifstream f(filePath);
    if (!f.is_open()) {
        std::cerr << "Error: Could not open game state file: " << filePath << std::endl;
        return std::nullopt;
    }

    json data;
    try {
        data = json::parse(f);
    } catch (json::parse_error& e) {
        std::cerr << "Error: Failed to parse JSON file: " << filePath << "\n" << e.what() << std::endl;
        return std::nullopt;
    }

    // Calculate grid dimensions from the Cells array
    int width = 0;
    int height = 0;
    if (data.contains("Cells") && data["Cells"].is_array()) {
        for (const auto& cell_json : data["Cells"]) {
            int x = get_optional_value(cell_json, "X", -1);
            int y = get_optional_value(cell_json, "Y", -1);
            if (x >= width) width = x + 1;
            if (y >= height) height = y + 1;
        }
    }

    if (width <= 0 || height <= 0) {
        std::cerr << "Error: Could not determine valid grid dimensions from JSON." << std::endl;
        return std::nullopt;
    }

    GameState gs(width, height);
    gs.tick = get_optional_value(data, "Tick", 0);

    // Populate cells and bitboards
    if (data.contains("Cells") && data["Cells"].is_array()) {
        for (const auto& cell_json : data["Cells"]) {
            int x = get_optional_value(cell_json, "X", -1);
            int y = get_optional_value(cell_json, "Y", -1);
            int content_int = get_optional_value(cell_json, "Content", 0);
            auto content = static_cast<CellContent>(content_int);

            if (gs.isValidPosition(x, y)) {
                gs.setCell(x, y, content);
                if (content == CellContent::Wall) gs.wallBoard.set(x, y);
                else if (content == CellContent::Pellet) gs.pelletBoard.set(x, y);
                else if (content == CellContent::PowerPellet) gs.powerUpBoard.set(x, y);
            }
        }
    }

    // Populate animals
    if (data.contains("Animals") && data["Animals"].is_array()) {
        for (const auto& animal_json : data["Animals"]) {
            Animal animal;
            animal.id = get_optional_value<std::string>(animal_json, "Id", "");
            animal.nickname = get_optional_value<std::string>(animal_json, "Nickname", "");
            animal.position = {get_optional_value(animal_json, "X", 0), get_optional_value(animal_json, "Y", 0)};
            animal.spawnPosition = {get_optional_value(animal_json, "SpawnX", 0), get_optional_value(animal_json, "SpawnY", 0)};
            animal.score = get_optional_value(animal_json, "Score", 0);
            animal.capturedCounter = get_optional_value(animal_json, "CapturedCounter", 0);
            animal.distanceCovered = get_optional_value(animal_json, "DistanceCovered", 0);
            animal.isViable = get_optional_value(animal_json, "IsViable", true);
            gs.animals.push_back(animal);

            if (animal.nickname == myBotNickname) {
                gs.myAnimalId = animal.id;
            }
        }
    }

    // Populate zookeepers
    if (data.contains("Zookeepers") && data["Zookeepers"].is_array()) {
        for (const auto& zk_json : data["Zookeepers"]) {
            Zookeeper zk;
            zk.id = get_optional_value<std::string>(zk_json, "Id", "");
            zk.nickname = get_optional_value<std::string>(zk_json, "Nickname", "");
            zk.position = {get_optional_value(zk_json, "X", 0), get_optional_value(zk_json, "Y", 0)};
            zk.spawnPosition = {get_optional_value(zk_json, "SpawnX", 0), get_optional_value(zk_json, "SpawnY", 0)};
            gs.zookeepers.push_back(zk);
        }
    }

    if (gs.myAnimalId.empty()) {
        std::cerr << "Warning: Bot with nickname '" << myBotNickname << "' not found in game state." << std::endl;
    }

    return gs;
}

}
