#pragma once

#include <vector>
#include <array>
#include <bitset>
#include <memory>
#include <unordered_map>
#include <cstdint>
#include <unordered_set>

enum class BotAction : int {
    None = 0,
    Up = 1,
    Down = 2,
    Left = 3,
    Right = 4,
    UseItem = 5
};

struct BotActionCommand {
    BotAction actionType;
    int targetX = 0;
    int targetY = 0;
};

enum class CellContent : int {
    Empty = 0,
    Wall = 1,
    Pellet = 2,
    Animal = 3,
    Zookeeper = 4,
    PowerPellet = 5,
    ChameleonCloak = 6,
    Scavenger = 7,
    BigMooseJuice = 8
};

enum class PowerUpType : int {
    None = 0,
    ChameleonCloak = 1,
    Scavenger = 2,
    BigMooseJuice = 3
};

struct Position {
    int x, y;
    
    Position() : x(0), y(0) {}
    Position(int x, int y) : x(x), y(y) {}
    
    bool operator==(const Position& other) const {
        return x == other.x && y == other.y;
    }
    
    bool operator!=(const Position& other) const {
        return !(*this == other);
    }
    
    Position operator+(const Position& other) const {
        return Position(x + other.x, y + other.y);
    }
    
    int manhattanDistance(const Position& other) const {
        return abs(x - other.x) + abs(y - other.y);
    }
};

// Hash function for Position
namespace std {
    template<>
    struct hash<Position> {
        size_t operator()(const Position& pos) const {
            // A common way to combine hashes for a struct
            auto h1 = std::hash<int>{}(pos.x);
            auto h2 = std::hash<int>{}(pos.y);
            return h1 ^ (h2 << 1); // Combine hashes
        }
    };
} // namespace std

struct Animal {
    std::string id;
    std::string nickname;
    Position position;
    Position spawnPosition;
    int score;
    int capturedCounter;
    int distanceCovered;
    bool isViable;
    PowerUpType heldPowerUp;
    int powerUpDuration;
    int scoreStreak;
        int ticksSinceLastPellet;
    bool isCaught = false;
    
    Animal() : score(0), capturedCounter(0), distanceCovered(0), 
               isViable(true), heldPowerUp(PowerUpType::None), 
               powerUpDuration(0), scoreStreak(1), ticksSinceLastPellet(0) {}
};

struct Zookeeper {
    std::string id;
    std::string nickname;
    Position position;
    Position spawnPosition;
    std::string targetAnimalId;
    int ticksSinceTargetUpdate;
    
    Zookeeper() : ticksSinceTargetUpdate(0) {}
};

class BitBoard {
private:
    static constexpr int MAX_SIZE = 64;
    std::bitset<MAX_SIZE * MAX_SIZE> bits;
    int width, height;
    
public:
    BitBoard(int w = 0, int h = 0) : width(w), height(h) {}
    
    void set(int x, int y, bool value = true) {
        if (x >= 0 && x < width && y >= 0 && y < height) {
            bits[y * width + x] = value;
        }
    }
    
    bool get(int x, int y) const {
        if (x >= 0 && x < width && y >= 0 && y < height) {
            return bits[y * width + x];
        }
        return false;
    }
    
    void clear() { bits.reset(); }
    int count() const { return static_cast<int>(bits.count()); }
    
    BitBoard operator&(const BitBoard& other) const {
        BitBoard result(width, height);
        result.bits = bits & other.bits;
        return result;
    }
    
    BitBoard operator|(const BitBoard& other) const {
        BitBoard result(width, height);
        result.bits = bits | other.bits;
        return result;
    }
};

class GameState {
private:
    int width, height;
    std::vector<std::vector<CellContent>> grid;
    // BitBoard members moved to public
    
public:
    int tick;
    std::vector<Animal> animals;
    std::vector<Zookeeper> zookeepers;
    std::string myAnimalId;
    BitBoard pelletBoard;
    BitBoard powerUpBoard;
    BitBoard wallBoard;

    int gridWidth = 0;
    int gridHeight = 0;
    int remainingTicks = 0;
        std::string gameMode;

    std::unordered_set<Position> visitedCells;
    
    GameState(int w = 0, int h = 0);
    
    // Core game state methods
    void initializeGrid(int w, int h);
    void setCell(int x, int y, CellContent content);
    // Game state queries
    bool isTerminal() const;
    bool isPlayerCaught(const std::string& playerId) const;
    bool isTraversable(int x, int y) const;
    bool isValidPosition(int x, int y) const;
    CellContent getCell(int x, int y) const;
    // BitBoard access
    const BitBoard& getPelletBoard() const { return pelletBoard; }
    const BitBoard& getPowerUpBoard() const { return powerUpBoard; }
    const BitBoard& getWallBoard() const { return wallBoard; }
    
    // Game logic
    std::vector<BotAction> getLegalActions(const std::string& animalId) const;
    void applyAction(const std::string& animalId, BotAction action);
    
    // Animal management
    Animal* getAnimal(const std::string& id);
    const Animal* getAnimal(const std::string& id) const;
    Animal* getMyAnimal() { return getAnimal(myAnimalId); }
    const Animal* getMyAnimal() const { return getAnimal(myAnimalId); }
    
    // Utility methods
    std::vector<Position> getNearbyPellets(const Position& pos, int radius) const;
    std::vector<Position> getNearbyPowerUps(const Position& pos, int radius) const;
    double calculatePelletDensity(const Position& center, int radius) const;
    int countPelletsInArea(const Position& center, int radius) const;
    
    // Zookeeper methods
    Position predictZookeeperPosition(const Zookeeper& zk, int ticksAhead) const;
    double getZookeeperThreat(const Position& pos) const;
    
    // Cloning and hashing
    std::unique_ptr<GameState> clone() const;
    uint64_t hash() const;
    
    // Getters
    int getWidth() const { return width; }
    int getHeight() const { return height; }
    int getTick() const { return tick; }
};