#include "GameState.h"
#include <algorithm>
#include <cmath>
#include <random>
#include <unordered_set>

GameState::GameState(int w, int h) : width(w), height(h), tick(0) {
    if (w > 0 && h > 0) {
        initializeGrid(w, h);
    }
}

void GameState::initializeGrid(int w, int h) {
    width = w;
    height = h;
    
    grid.resize(height);
    for (int y = 0; y < height; ++y) {
        grid[y].resize(width, CellContent::Empty);
    }
    
    pelletBoard = BitBoard(width, height);
    powerUpBoard = BitBoard(width, height);
    wallBoard = BitBoard(width, height);
}

void GameState::setCell(int x, int y, CellContent content) {
    if (!isValidPosition(x, y)) return;
    
    grid[y][x] = content;
    
    // Update bitboards
    pelletBoard.set(x, y, content == CellContent::Pellet || content == CellContent::PowerPellet);
    powerUpBoard.set(x, y, content == CellContent::ChameleonCloak || 
                           content == CellContent::Scavenger || 
                           content == CellContent::BigMooseJuice);
    wallBoard.set(x, y, content == CellContent::Wall);
}

CellContent GameState::getCell(int x, int y) const {
    if (!isValidPosition(x, y)) return CellContent::Wall;
    return grid[y][x];
}

bool GameState::isValidPosition(int x, int y) const {
    return x >= 0 && x < width && y >= 0 && y < height;
}

bool GameState::isTraversable(int x, int y) const {
    if (!isValidPosition(x, y)) return false;
    return grid[y][x] != CellContent::Wall;
}

std::vector<BotAction> GameState::getLegalActions(const std::string& animalId) const {
    std::vector<BotAction> actions;
    
    const Animal* animal = getAnimal(animalId);
    if (!animal) return actions;
    
    // Check movement actions
    Position pos = animal->position;
    
    if (isTraversable(pos.x, pos.y - 1)) actions.push_back(BotAction::Up);
    if (isTraversable(pos.x, pos.y + 1)) actions.push_back(BotAction::Down);
    if (isTraversable(pos.x - 1, pos.y)) actions.push_back(BotAction::Left);
    if (isTraversable(pos.x + 1, pos.y)) actions.push_back(BotAction::Right);
    
    // Check UseItem action
    if (animal->heldPowerUp != PowerUpType::None) {
        actions.push_back(BotAction::UseItem);
    }
    
    return actions;
}

GameState GameState::applyAction(const std::string& animalId, BotAction action) const {
    GameState newState = *this;
    newState.tick++;
    
    Animal* animal = newState.getAnimal(animalId);
    if (!animal) return newState;
    
    Position oldPos = animal->position;
    Position newPos = oldPos;
    
    // Apply movement
    switch (action) {
        case BotAction::Up:
            newPos.y--;
            break;
        case BotAction::Down:
            newPos.y++;
            break;
        case BotAction::Left:
            newPos.x--;
            break;
        case BotAction::Right:
            newPos.x++;
            break;
        case BotAction::UseItem:
            // Handle power-up usage
            if (animal->heldPowerUp != PowerUpType::None) {
                switch (animal->heldPowerUp) {
                    case PowerUpType::ChameleonCloak:
                        animal->powerUpDuration = 20;
                        break;
                    case PowerUpType::Scavenger:
                        animal->powerUpDuration = 5;
                        // Collect all pellets in 11x11 area
                        for (int dx = -5; dx <= 5; dx++) {
                            for (int dy = -5; dy <= 5; dy++) {
                                int px = animal->position.x + dx;
                                int py = animal->position.y + dy;
                                if (newState.isValidPosition(px, py) && 
                                    newState.getCell(px, py) == CellContent::Pellet) {
                                    newState.setCell(px, py, CellContent::Empty);
                                    animal->score += animal->scoreStreak;
                                    animal->ticksSinceLastPellet = 0;
                                }
                            }
                        }
                        break;
                    case PowerUpType::BigMooseJuice:
                        animal->powerUpDuration = 5;
                        break;
                }
                animal->heldPowerUp = PowerUpType::None;
            }
            return newState;
    }
    
    // Validate movement
    if (!newState.isTraversable(newPos.x, newPos.y)) {
        return newState; // Invalid move, no change
    }
    
    // Update position
    animal->position = newPos;
    animal->distanceCovered++;
    
    // Handle cell content at new position
    CellContent cellContent = newState.getCell(newPos.x, newPos.y);
    switch (cellContent) {
        case CellContent::Pellet:
            {
                int pelletValue = animal->scoreStreak;
                if (animal->powerUpDuration > 0 && animal->heldPowerUp == PowerUpType::BigMooseJuice) {
                    pelletValue *= 3;
                }
                animal->score += pelletValue;
                animal->ticksSinceLastPellet = 0;
                animal->scoreStreak = std::min(4, animal->scoreStreak + 1);
                newState.setCell(newPos.x, newPos.y, CellContent::Empty);
            }
            break;
            
        case CellContent::PowerPellet:
            {
                int pelletValue = 10 * animal->scoreStreak;
                if (animal->powerUpDuration > 0 && animal->heldPowerUp == PowerUpType::BigMooseJuice) {
                    pelletValue *= 3;
                }
                animal->score += pelletValue;
                animal->ticksSinceLastPellet = 0;
                animal->scoreStreak = std::min(4, animal->scoreStreak + 1);
                newState.setCell(newPos.x, newPos.y, CellContent::Empty);
            }
            break;
            
        case CellContent::ChameleonCloak:
            animal->heldPowerUp = PowerUpType::ChameleonCloak;
            newState.setCell(newPos.x, newPos.y, CellContent::Empty);
            break;
            
        case CellContent::Scavenger:
            animal->heldPowerUp = PowerUpType::Scavenger;
            newState.setCell(newPos.x, newPos.y, CellContent::Empty);
            break;
            
        case CellContent::BigMooseJuice:
            animal->heldPowerUp = PowerUpType::BigMooseJuice;
            newState.setCell(newPos.x, newPos.y, CellContent::Empty);
            break;
    }
    
    // Update power-up durations
    if (animal->powerUpDuration > 0) {
        animal->powerUpDuration--;
    }
    
    // Update score streak
    animal->ticksSinceLastPellet++;
    if (animal->ticksSinceLastPellet >= 3) {
        animal->scoreStreak = 1;
    }
    
    // Simulate zookeeper movement and capture logic
    for (auto& zookeeper : newState.zookeepers) {
        // Simple zookeeper AI - move towards target
        if (!zookeeper.targetAnimalId.empty()) {
            const Animal* target = newState.getAnimal(zookeeper.targetAnimalId);
            if (target) {
                Position zkPos = zookeeper.position;
                Position targetPos = target->position;
                
                // Move one step towards target
                if (targetPos.x > zkPos.x && newState.isTraversable(zkPos.x + 1, zkPos.y)) {
                    zookeeper.position.x++;
                } else if (targetPos.x < zkPos.x && newState.isTraversable(zkPos.x - 1, zkPos.y)) {
                    zookeeper.position.x--;
                } else if (targetPos.y > zkPos.y && newState.isTraversable(zkPos.x, zkPos.y + 1)) {
                    zookeeper.position.y++;
                } else if (targetPos.y < zkPos.y && newState.isTraversable(zkPos.x, zkPos.y - 1)) {
                    zookeeper.position.y--;
                }
                
                // Check for capture
                if (zookeeper.position == target->position) {
                    Animal* capturedAnimal = newState.getAnimal(zookeeper.targetAnimalId);
                    if (capturedAnimal && capturedAnimal->powerUpDuration == 0) { // Not invisible
                        capturedAnimal->position = capturedAnimal->spawnPosition;
                        capturedAnimal->capturedCounter++;
                        capturedAnimal->score = static_cast<int>(capturedAnimal->score * 0.8); // 20% penalty
                        capturedAnimal->scoreStreak = 1;
                        capturedAnimal->ticksSinceLastPellet = 0;
                    }
                }
            }
        }
        
        // Update target every 20 ticks
        zookeeper.ticksSinceTargetUpdate++;
        if (zookeeper.ticksSinceTargetUpdate >= 20) {
            zookeeper.ticksSinceTargetUpdate = 0;
            
            // Find nearest viable animal
            double minDistance = std::numeric_limits<double>::max();
            std::string nearestAnimalId;
            
            for (const auto& a : newState.animals) {
                if (a.isViable && a.position != a.spawnPosition) {
                    double distance = zookeeper.position.manhattanDistance(a.position);
                    if (distance < minDistance) {
                        minDistance = distance;
                        nearestAnimalId = a.id;
                    }
                }
            }
            
            zookeeper.targetAnimalId = nearestAnimalId;
        }
    }
    
    return newState;
}

bool GameState::isTerminal() const {
    // Game is terminal if no pellets remain or time limit reached
    return pelletBoard.count() == 0 || tick >= 1000; // Assuming 1000 tick limit
}

double GameState::evaluate(const std::string& animalId) const {
    const Animal* animal = getAnimal(animalId);
    if (!animal) return 0.0;
    
    double score = animal->score;
    
    // Penalty for being captured
    score -= animal->capturedCounter * 50;
    
    // Bonus for score streak
    score += animal->scoreStreak * 10;
    
    // Bonus for being far from zookeepers
    double minZookeeperDistance = std::numeric_limits<double>::max();
    for (const auto& zk : zookeepers) {
        double distance = animal->position.manhattanDistance(zk.position);
        minZookeeperDistance = std::min(minZookeeperDistance, distance);
    }
    
    if (minZookeeperDistance < 5) {
        score -= (5 - minZookeeperDistance) * 20; // Penalty for being close to zookeeper
    }
    
    return score;
}

Animal* GameState::getAnimal(const std::string& id) {
    auto it = std::find_if(animals.begin(), animals.end(),
                          [&id](const Animal& a) { return a.id == id; });
    return it != animals.end() ? &(*it) : nullptr;
}

const Animal* GameState::getAnimal(const std::string& id) const {
    auto it = std::find_if(animals.begin(), animals.end(),
                          [&id](const Animal& a) { return a.id == id; });
    return it != animals.end() ? &(*it) : nullptr;
}

std::vector<Position> GameState::getNearbyPellets(const Position& pos, int radius) const {
    std::vector<Position> pellets;
    
    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            int x = pos.x + dx;
            int y = pos.y + dy;
            
            if (isValidPosition(x, y) && 
                (getCell(x, y) == CellContent::Pellet || getCell(x, y) == CellContent::PowerPellet)) {
                pellets.emplace_back(x, y);
            }
        }
    }
    
    return pellets;
}

std::vector<Position> GameState::getNearbyPowerUps(const Position& pos, int radius) const {
    std::vector<Position> powerUps;
    
    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            int x = pos.x + dx;
            int y = pos.y + dy;
            
            if (isValidPosition(x, y)) {
                CellContent content = getCell(x, y);
                if (content == CellContent::ChameleonCloak || 
                    content == CellContent::Scavenger || 
                    content == CellContent::BigMooseJuice) {
                    powerUps.emplace_back(x, y);
                }
            }
        }
    }
    
    return powerUps;
}

double GameState::calculatePelletDensity(const Position& center, int radius) const {
    int pelletCount = 0;
    int totalCells = 0;
    
    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            int x = center.x + dx;
            int y = center.y + dy;
            
            if (isValidPosition(x, y)) {
                totalCells++;
                if (getCell(x, y) == CellContent::Pellet || getCell(x, y) == CellContent::PowerPellet) {
                    pelletCount++;
                }
            }
        }
    }
    
    return totalCells > 0 ? static_cast<double>(pelletCount) / totalCells : 0.0;
}

int GameState::countPelletsInArea(const Position& center, int radius) const {
    int count = 0;
    
    for (int dx = -radius; dx <= radius; dx++) {
        for (int dy = -radius; dy <= radius; dy++) {
            int x = center.x + dx;
            int y = center.y + dy;
            
            if (isValidPosition(x, y) && 
                (getCell(x, y) == CellContent::Pellet || getCell(x, y) == CellContent::PowerPellet)) {
                count++;
            }
        }
    }
    
    return count;
}

Position GameState::predictZookeeperPosition(const Zookeeper& zk, int ticksAhead) const {
    Position predictedPos = zk.position;
    
    if (zk.targetAnimalId.empty()) return predictedPos;
    
    const Animal* target = getAnimal(zk.targetAnimalId);
    if (!target) return predictedPos;
    
    // Simple prediction: assume zookeeper moves directly towards target
    for (int i = 0; i < ticksAhead; i++) {
        Position targetPos = target->position;
        
        if (targetPos.x > predictedPos.x && isTraversable(predictedPos.x + 1, predictedPos.y)) {
            predictedPos.x++;
        } else if (targetPos.x < predictedPos.x && isTraversable(predictedPos.x - 1, predictedPos.y)) {
            predictedPos.x--;
        } else if (targetPos.y > predictedPos.y && isTraversable(predictedPos.x, predictedPos.y + 1)) {
            predictedPos.y++;
        } else if (targetPos.y < predictedPos.y && isTraversable(predictedPos.x, predictedPos.y - 1)) {
            predictedPos.y--;
        }
    }
    
    return predictedPos;
}

double GameState::getZookeeperThreat(const Position& pos) const {
    double maxThreat = 0.0;
    
    for (const auto& zk : zookeepers) {
        double distance = pos.manhattanDistance(zk.position);
        double threat = std::max(0.0, 10.0 - distance); // Threat decreases with distance
        maxThreat = std::max(maxThreat, threat);
    }
    
    return maxThreat;
}

std::unique_ptr<GameState> GameState::clone() const {
    return std::make_unique<GameState>(*this);
}

uint64_t GameState::hash() const {
    uint64_t hash = 0;
    
    // Hash tick
    hash ^= std::hash<int>()(tick) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    
    // Hash animal positions and scores
    for (const auto& animal : animals) {
        hash ^= std::hash<int>()(animal.position.x) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
        hash ^= std::hash<int>()(animal.position.y) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
        hash ^= std::hash<int>()(animal.score) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    }
    
    // Hash zookeeper positions
    for (const auto& zk : zookeepers) {
        hash ^= std::hash<int>()(zk.position.x) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
        hash ^= std::hash<int>()(zk.position.y) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
    }
    
    return hash;
}