#include "GameState.h"
#include "fmt/core.h" // Added for fmt::println
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
    bool isWall = (content == CellContent::Wall);
    wallBoard.set(x, y, isWall);
    // if (isWall) { /* Wall logging removed */ }
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
    return !wallBoard.get(x, y);
}

std::vector<BotAction> GameState::getLegalActions(const std::string& animalId) const {
    std::vector<BotAction> actions;
    
    const Animal* animal = getAnimal(animalId);
    if (!animal) {
        // fmt::println("DEBUG_GameState: getLegalActions called for animalId '{}', but animal was NOT FOUND.", animalId);
        return actions;
    }
    // fmt::println("DEBUG_GameState: getLegalActions called for animalId '{}', animal FOUND. Position: ({}, {}). Held PowerUp: {}", 
    //              animalId, animal->position.x, animal->position.y, static_cast<int>(animal->heldPowerUp));
    
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
    
    // fmt::print("DEBUG_GameState: Generated {} legal actions for animalId '{}': [", actions.size(), animalId);
    // for(size_t i = 0; i < actions.size(); ++i) {
    //     fmt::print("{}{}", static_cast<int>(actions[i]), (i == actions.size() - 1) ? "" : ", ");
    // }
    // fmt::println("]");
    return actions;
}

void GameState::applyAction(const std::string& animalId, BotAction action) {
    tick++;

    Animal* animal = this->getAnimal(animalId);
    if (!animal) return;

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
                                if (this->isValidPosition(px, py) && 
                                    this->getCell(px, py) == CellContent::Pellet) {
                                    this->setCell(px, py, CellContent::Empty);
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
            return;
    }
    
    // Validate movement
    if (!this->isTraversable(newPos.x, newPos.y)) {
        return; // Invalid move, no change
    }
    
    // Update position
        animal->position = newPos;
    visitedCells.insert(newPos);
    animal->distanceCovered++;
    
    // Handle cell content at new position
    CellContent cellContent = this->getCell(newPos.x, newPos.y);
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
                this->setCell(newPos.x, newPos.y, CellContent::Empty);
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
                this->setCell(newPos.x, newPos.y, CellContent::Empty);
            }
            break;
            
        case CellContent::ChameleonCloak:
            animal->heldPowerUp = PowerUpType::ChameleonCloak;
            this->setCell(newPos.x, newPos.y, CellContent::Empty);
            break;
            
        case CellContent::Scavenger:
            animal->heldPowerUp = PowerUpType::Scavenger;
            this->setCell(newPos.x, newPos.y, CellContent::Empty);
            break;
            
        case CellContent::BigMooseJuice:
            animal->heldPowerUp = PowerUpType::BigMooseJuice;
            this->setCell(newPos.x, newPos.y, CellContent::Empty);
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
    for (auto& zookeeper : this->zookeepers) {
        // Simple zookeeper AI - move towards target
        if (!zookeeper.targetAnimalId.empty()) {
            const Animal* target = this->getAnimal(zookeeper.targetAnimalId);
            if (target) {
                Position zkPos = zookeeper.position;
                Position targetPos = target->position;
                
                // Move one step towards target
                if (targetPos.x > zkPos.x && this->isTraversable(zkPos.x + 1, zkPos.y)) {
                    zookeeper.position.x++;
                } else if (targetPos.x < zkPos.x && this->isTraversable(zkPos.x - 1, zkPos.y)) {
                    zookeeper.position.x--;
                } else if (targetPos.y > zkPos.y && this->isTraversable(zkPos.x, zkPos.y + 1)) {
                    zookeeper.position.y++;
                } else if (targetPos.y < zkPos.y && this->isTraversable(zkPos.x, zkPos.y - 1)) {
                    zookeeper.position.y--;
                }
                
                // Check for capture
                if (zookeeper.position == target->position) {
                    Animal* capturedAnimal = this->getAnimal(zookeeper.targetAnimalId);
                    if (capturedAnimal && capturedAnimal->powerUpDuration == 0) { // Not invisible
                        capturedAnimal->position = capturedAnimal->spawnPosition;
                        capturedAnimal->capturedCounter++;
                        capturedAnimal->score = static_cast<int>(capturedAnimal->score * 0.8); // 20% penalty
                        capturedAnimal->scoreStreak = 1;
                                                capturedAnimal->ticksSinceLastPellet = 0;
                        capturedAnimal->isCaught = true;
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
            
            for (const auto& a : this->animals) {
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
}

bool GameState::isPlayerCaught(const std::string& playerId) const {
    const Animal* animal = getAnimal(playerId);
    return animal && animal->isCaught;
}

bool GameState::isTerminal() const {
    const Animal* myAnimal = getMyAnimal();
    if (myAnimal && myAnimal->isCaught) {
        return true;
    }
    return pelletBoard.count() == 0 || tick >= 1000; // Assuming 1000 tick limit
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
    auto clonedState = std::make_unique<GameState>(width, height);
    
    // Copy simple members
    clonedState->tick = this->tick;
    clonedState->myAnimalId = this->myAnimalId;
    clonedState->gridWidth = this->gridWidth;
    clonedState->gridHeight = this->gridHeight;
    clonedState->remainingTicks = this->remainingTicks;
    clonedState->gameMode = this->gameMode;

    // Copy complex members
    clonedState->grid = this->grid;
    clonedState->animals = this->animals;
    clonedState->zookeepers = this->zookeepers;
    clonedState->pelletBoard = this->pelletBoard;
    clonedState->powerUpBoard = this->powerUpBoard;
    clonedState->wallBoard = this->wallBoard;
    
    // Copy new members
    clonedState->visitedCells = this->visitedCells;

    return clonedState;
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