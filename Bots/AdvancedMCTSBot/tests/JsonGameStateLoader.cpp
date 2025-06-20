#include "JsonGameStateLoader.h"
#include <climits>
#include <stack>
#include <unordered_set>
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

StateAnalysis JsonGameStateLoader::analyzeState(const GameState& gs, const std::string& myBotNickname) {
    StateAnalysis sa{};

    const Animal* me = nullptr;
    for (const auto& a : gs.animals) {
        if (a.nickname == myBotNickname) { me = &a; break; }
    }
    if (!me) {
        return sa; // default (invalid)
    }
    sa.myPos = me->position;
    sa.score = me->score;

    auto countConsecutive = [&](int dx,int dy){
        int count=0;Position cur=sa.myPos;cur.x+=dx;cur.y+=dy;
        while(gs.isValidPosition(cur.x,cur.y) && gs.getCell(cur.x,cur.y)==CellContent::Pellet){++count;cur.x+=dx;cur.y+=dy;}
        return count;
    };

    auto checkPelletLine = [&](int dx, int dy, int& pelletsLine){
        Position cur = sa.myPos;
        for (int step = 1; step <= 3; ++step) {
            cur.x += dx; cur.y += dy;
            if (!gs.isValidPosition(cur.x, cur.y)) break;
            if (gs.getCell(cur.x, cur.y) == CellContent::Pellet) {
                if (step == 1) {
                    if (dx==0 && dy==-1) sa.pelletUp = true;
                    if (dx==-1 && dy==0) sa.pelletLeft = true;
                    if (dx==1 && dy==0) sa.pelletRight = true;
                    if (dx==0 && dy==1) sa.pelletDown = true;
                }
                ++pelletsLine;
            }
        }
    };

    checkPelletLine(0,-1, sa.pelletsUpTo3);
    checkPelletLine(-1,0, sa.pelletsLeftTo3);
    checkPelletLine(1,0, sa.pelletsRightTo3);
    checkPelletLine(0,1, sa.pelletsDownTo3);

    auto countConnected = [&](Position start){
        std::stack<Position> st; std::unordered_set<uint32_t> vis;
        auto key=[&](Position p){return (p.y<<8)|p.x;};
        st.push(start); vis.insert(key(start)); int cnt=0;
        while(!st.empty()){
            Position cur=st.top();st.pop();++cnt;
            const int dx[4]={1,-1,0,0}; const int dy[4]={0,0,1,-1};
            for(int k=0;k<4;++k){int nx=cur.x+dx[k], ny=cur.y+dy[k];
                if(!gs.isValidPosition(nx,ny)) continue;
                if(gs.getCell(nx,ny)!=CellContent::Pellet) continue;
                uint32_t k2=(ny<<8)|nx; if(vis.count(k2)) continue;
                vis.insert(k2); st.push({nx,ny});
            }
        }
        return cnt;
    };

    if(sa.pelletUp){ sa.consecutivePelletsUp = countConnected({sa.myPos.x, sa.myPos.y-1}); }
    if(sa.pelletLeft){ sa.consecutivePelletsLeft = countConnected({sa.myPos.x-1, sa.myPos.y}); }
    if(sa.pelletRight){ sa.consecutivePelletsRight = countConnected({sa.myPos.x+1, sa.myPos.y}); }
    if(sa.pelletDown){ sa.consecutivePelletsDown = countConnected({sa.myPos.x, sa.myPos.y+1}); }

    // Quadrant pellet counts
    int midX = gs.getWidth()/2;
    int midY = gs.getHeight()/2;
    for(int y=0;y<gs.getHeight();++y){
        for(int x=0;x<gs.getWidth();++x){
            if(gs.getCell(x,y)==CellContent::Pellet){
                int quad = (x>=midX) ? (y>=midY?3:1) : (y>=midY?2:0);
                ++sa.pelletsPerQuadrant[quad];
            }
        }
    }
    sa.currentQuadrant = (sa.myPos.x>=midX)? (sa.myPos.y>=midY?3:1) : (sa.myPos.y>=midY?2:0);

    // nearest zookeeper distance
    for (const auto& zk : gs.zookeepers) {
        int dist = std::abs(zk.position.x - sa.myPos.x) + std::abs(zk.position.y - sa.myPos.y);
        if (dist < sa.nearestZookeeperDist) {
            sa.nearestZookeeperDist = dist;
            sa.nearestZookeeperPos = zk.position;
        }
    }

    return sa;
}

std::optional<StateAnalysis> JsonGameStateLoader::analyzeStateFromFile(const std::string& filePath, const std::string& myBotNickname) {
    auto gsOpt = JsonGameStateLoader::loadStateFromFile(filePath, myBotNickname);
    if (!gsOpt) return std::nullopt;
    return analyzeState(*gsOpt, myBotNickname);
}

}
