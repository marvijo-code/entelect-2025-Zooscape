#include "Heuristics.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <fstream>
#include <iostream>

// PelletDistanceHeuristic implementation
double PelletDistanceHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    // Calculate position after action
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0; // No movement
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    // Find nearest pellet
    double minDistance = std::numeric_limits<double>::max();
    auto nearbyPellets = state.getNearbyPellets(newPos, 10);
    
    for (const auto& pelletPos : nearbyPellets) {
        double distance = newPos.manhattanDistance(pelletPos);
        minDistance = std::min(minDistance, distance);
    }
    
    if (minDistance == std::numeric_limits<double>::max()) {
        return 0.0; // No pellets found
    }
    
    // Return inverse distance (closer is better)
    return weight * (20.0 - minDistance) / 20.0;
}

// PelletDensityHeuristic implementation
double PelletDensityHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    double density = state.calculatePelletDensity(newPos, searchRadius);
    return weight * density * 100.0;
}

// ScoreStreakHeuristic implementation
double ScoreStreakHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: 
            // Using power-ups can be beneficial for maintaining streaks
            if (animal->heldPowerUp == PowerUpType::Scavenger) {
                return weight * 50.0; // High value for scavenger usage
            }
            return weight * 10.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    // Check if moving to a pellet
    CellContent content = state.getCell(newPos.x, newPos.y);
    if (content == CellContent::Pellet || content == CellContent::PowerPellet) {
        // Bonus based on current streak
        double streakBonus = animal->scoreStreak * 10.0;
        
        // Extra bonus if streak is about to reset
        if (animal->ticksSinceLastPellet >= 2) {
            streakBonus += 30.0; // Urgent pellet collection
        }
        
        return weight * streakBonus;
    }
    
    // Penalty for moves that don't collect pellets when streak is at risk
    if (animal->ticksSinceLastPellet >= 2) {
        return weight * -20.0;
    }
    
    return 0.0;
}

// ZookeeperAvoidanceHeuristic implementation
double ZookeeperAvoidanceHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem:
            // Using chameleon cloak is highly valuable when near zookeepers
            if (animal->heldPowerUp == PowerUpType::ChameleonCloak) {
                double threat = state.getZookeeperThreat(animal->position);
                return weight * threat * 20.0;
            }
            return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    double minDistance = std::numeric_limits<double>::max();
    for (const auto& zookeeper : state.zookeepers) {
        double distance = newPos.manhattanDistance(zookeeper.position);
        minDistance = std::min(minDistance, distance);
    }
    
    if (minDistance < dangerRadius) {
        // Strong penalty for being too close
        double penalty = (dangerRadius - minDistance) * 20.0;
        return weight * -penalty;
    }
    
    // Small bonus for maintaining safe distance
    return weight * std::min(10.0, minDistance);
}

// ZookeeperPredictionHeuristic implementation
double ZookeeperPredictionHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    double totalThreat = 0.0;
    for (const auto& zookeeper : state.zookeepers) {
        for (int step = 1; step <= predictionSteps; ++step) {
            Position predictedPos = state.predictZookeeperPosition(zookeeper, step);
            double distance = newPos.manhattanDistance(predictedPos);
            
            if (distance < 3) {
                // High threat if zookeeper will be very close
                totalThreat += (3.0 - distance) * (predictionSteps - step + 1) * 10.0;
            }
        }
    }
    
    return weight * -totalThreat;
}

// PowerUpCollectionHeuristic implementation
double PowerUpCollectionHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    CellContent content = state.getCell(newPos.x, newPos.y);
    double powerUpValue = 0.0;
    
    switch (content) {
        case CellContent::ChameleonCloak:
            powerUpValue = 40.0; // High value for safety
            break;
        case CellContent::Scavenger:
            powerUpValue = 60.0; // Very high value for pellet collection
            break;
        case CellContent::BigMooseJuice:
            powerUpValue = 50.0; // High value for score multiplication
            break;
        default:
            // Check nearby power-ups
            auto nearbyPowerUps = state.getNearbyPowerUps(newPos, 5);
            if (!nearbyPowerUps.empty()) {
                double minDistance = std::numeric_limits<double>::max();
                for (const auto& powerUpPos : nearbyPowerUps) {
                    double distance = newPos.manhattanDistance(powerUpPos);
                    minDistance = std::min(minDistance, distance);
                }
                powerUpValue = (5.0 - minDistance) * 5.0;
            }
            break;
    }
    
    return weight * powerUpValue;
}

// PowerUpUsageHeuristic implementation
double PowerUpUsageHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal || action != BotAction::UseItem) return 0.0;
    
    double usageValue = 0.0;
    
    switch (animal->heldPowerUp) {
        case PowerUpType::ChameleonCloak:
            {
                double threat = state.getZookeeperThreat(animal->position);
                usageValue = threat * 30.0; // Use when threatened
            }
            break;
            
        case PowerUpType::Scavenger:
            {
                int pelletsInArea = state.countPelletsInArea(animal->position, 5);
                usageValue = pelletsInArea * 15.0; // Use when many pellets nearby
            }
            break;
            
        case PowerUpType::BigMooseJuice:
            {
                int pelletsInArea = state.countPelletsInArea(animal->position, 3);
                double streakMultiplier = animal->scoreStreak;
                usageValue = pelletsInArea * streakMultiplier * 8.0; // Use when can maximize score
            }
            break;
            
        default:
            return 0.0;
    }
    
    return weight * usageValue;
}

// CenterControlHeuristic implementation
double CenterControlHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    Position center(state.getWidth() / 2, state.getHeight() / 2);
    double distanceToCenter = newPos.manhattanDistance(center);
    double maxDistance = state.getWidth() + state.getHeight();
    
    // Prefer moderate distance from center (not too close due to zookeeper, not too far)
    double optimalDistance = maxDistance * 0.3;
    double deviation = std::abs(distanceToCenter - optimalDistance);
    
    return weight * (maxDistance - deviation) / maxDistance * 10.0;
}

// WallAvoidanceHeuristic implementation
double WallAvoidanceHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    // Count traversable neighbors
    int traversableNeighbors = 0;
    for (int dx = -1; dx <= 1; dx++) {
        for (int dy = -1; dy <= 1; dy++) {
            if (dx == 0 && dy == 0) continue;
            if (state.isTraversable(newPos.x + dx, newPos.y + dy)) {
                traversableNeighbors++;
            }
        }
    }
    
    // Prefer positions with more escape routes
    return weight * traversableNeighbors * 2.0;
}

// MovementConsistencyHeuristic implementation
double MovementConsistencyHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    auto it = lastActions.find(playerId);
    if (it == lastActions.end()) {
        lastActions[playerId] = action;
        return 0.0;
    }
    
    BotAction lastAction = it->second;
    lastActions[playerId] = action;
    
    // Bonus for continuing in same direction
    if (action == lastAction && action != BotAction::UseItem) {
        return weight * 5.0;
    }
    
    // Penalty for reversing direction
    bool isReverse = (action == BotAction::Up && lastAction == BotAction::Down) ||
                    (action == BotAction::Down && lastAction == BotAction::Up) ||
                    (action == BotAction::Left && lastAction == BotAction::Right) ||
                    (action == BotAction::Right && lastAction == BotAction::Left);
    
    if (isReverse) {
        return weight * -10.0;
    }
    
    return 0.0;
}

// TerritoryControlHeuristic implementation
double TerritoryControlHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    double controlValue = HeuristicUtils::calculateAreaControl(newPos, controlRadius, state, playerId);
    return weight * controlValue;
}

// OpponentBlockingHeuristic implementation
double OpponentBlockingHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    double blockingValue = 0.0;
    
    // Check if this position blocks opponents from valuable pellets
    for (const auto& opponent : state.animals) {
        if (opponent.id == playerId) continue;
        
        auto nearbyPellets = state.getNearbyPellets(opponent.position, 5);
        for (const auto& pelletPos : nearbyPellets) {
            double opponentDistance = opponent.position.manhattanDistance(pelletPos);
            double myDistance = newPos.manhattanDistance(pelletPos);
            
            if (myDistance < opponentDistance) {
                blockingValue += (opponentDistance - myDistance) * 2.0;
            }
        }
    }
    
    return weight * blockingValue;
}

// EndgameHeuristic implementation
double EndgameHeuristic::evaluate(const GameState& state, const std::string& playerId, BotAction action) const {
    // Determine if we're in endgame
    int totalPellets = state.getPelletBoard().count();
    int maxPellets = state.getWidth() * state.getHeight(); // Rough estimate
    double pelletRatio = static_cast<double>(totalPellets) / maxPellets;
    
    if (pelletRatio > endgameThreshold) {
        return 0.0; // Not in endgame
    }
    
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return 0.0;
    
    Position newPos = animal->position;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        case BotAction::UseItem: return 0.0;
    }
    
    if (!state.isValidPosition(newPos.x, newPos.y)) return -1000.0;
    
    // In endgame, prioritize remaining pellets heavily
    CellContent content = state.getCell(newPos.x, newPos.y);
    if (content == CellContent::Pellet || content == CellContent::PowerPellet) {
        return weight * 100.0; // Very high value for remaining pellets
    }
    
    // Also prioritize being close to remaining pellets
    auto nearbyPellets = state.getNearbyPellets(newPos, 10);
    if (!nearbyPellets.empty()) {
        double minDistance = std::numeric_limits<double>::max();
        for (const auto& pelletPos : nearbyPellets) {
            double distance = newPos.manhattanDistance(pelletPos);
            minDistance = std::min(minDistance, distance);
        }
        return weight * (10.0 - minDistance) * 5.0;
    }
    
    return 0.0;
}

// ConsecutivePelletHeuristic implementation
double ConsecutivePelletHeuristic::evaluate(const GameState& state,
                                            const std::string& playerId,
                                            BotAction action) const {
    const Animal* me = state.getAnimal(playerId);
    if (!me) return 0.0;

    // Project next position after action
    Position pos = me->position;
    switch (action) {
        case BotAction::Up:    pos.y--; break;
        case BotAction::Down:  pos.y++; break;
        case BotAction::Left:  pos.x--; break;
        case BotAction::Right: pos.x++; break;
        default: break;
    }
    if (!state.isValidPosition(pos.x, pos.y) || !state.isTraversable(pos.x, pos.y)) {
        return 0.0;
    }

    // Count consecutive pellets starting from new position continuing in same direction
    int dx = 0, dy = 0;
    switch (action) {
        case BotAction::Up:    dy = -1; break;
        case BotAction::Down:  dy = 1; break;
        case BotAction::Left:  dx = -1; break;
        case BotAction::Right: dx = 1; break;
        default: break;
    }

    if (dx == 0 && dy == 0) return 0.0; // UseItem or None

    int consecutive = 0;
    Position cur = pos;
    for (int step = 0; step < maxLookahead; ++step) {
        if (!state.isValidPosition(cur.x, cur.y) || !state.isTraversable(cur.x, cur.y)) break;
        CellContent content = state.getCell(cur.x, cur.y);
        if (content == CellContent::Pellet || content == CellContent::PowerPellet) {
            ++consecutive;
        } else {
            break;
        }
        cur.x += dx;
        cur.y += dy;
    }

    return weight * static_cast<double>(consecutive);
}

// HeuristicsEngine implementation
HeuristicsEngine::HeuristicsEngine(bool logging) : enableLogging(logging) {
    // Initialize with default heuristics
    addHeuristic(std::make_unique<PelletDistanceHeuristic>());
    addHeuristic(std::make_unique<PelletDensityHeuristic>());
    addHeuristic(std::make_unique<ScoreStreakHeuristic>());
    addHeuristic(std::make_unique<ConsecutivePelletHeuristic>());
    addHeuristic(std::make_unique<ZookeeperAvoidanceHeuristic>());
    addHeuristic(std::make_unique<ZookeeperPredictionHeuristic>());
    addHeuristic(std::make_unique<PowerUpCollectionHeuristic>());
    addHeuristic(std::make_unique<PowerUpUsageHeuristic>());
    addHeuristic(std::make_unique<CenterControlHeuristic>());
    addHeuristic(std::make_unique<WallAvoidanceHeuristic>());
    addHeuristic(std::make_unique<MovementConsistencyHeuristic>());
    addHeuristic(std::make_unique<TerritoryControlHeuristic>());
    addHeuristic(std::make_unique<OpponentBlockingHeuristic>());
    addHeuristic(std::make_unique<EndgameHeuristic>());
}

void HeuristicsEngine::addHeuristic(std::unique_ptr<IHeuristic> heuristic) {
    heuristicWeights[heuristic->getName()] = heuristic->getWeight();
    heuristics.push_back(std::move(heuristic));
}

void HeuristicsEngine::removeHeuristic(const std::string& name) {
    heuristics.erase(
        std::remove_if(heuristics.begin(), heuristics.end(),
            [&name](const std::unique_ptr<IHeuristic>& h) {
                return h->getName() == name;
            }),
        heuristics.end());
    heuristicWeights.erase(name);
}

void HeuristicsEngine::setHeuristicWeight(const std::string& name, double weight) {
    heuristicWeights[name] = weight;
    for (auto& heuristic : heuristics) {
        if (heuristic->getName() == name) {
            heuristic->setWeight(weight);
            break;
        }
    }
}

double HeuristicsEngine::getHeuristicWeight(const std::string& name) const {
    auto it = heuristicWeights.find(name);
    return it != heuristicWeights.end() ? it->second : 0.0;
}

double HeuristicsEngine::evaluateAction(const GameState& state, const std::string& playerId, BotAction action) const {
    double totalScore = 0.0;
    
    for (const auto& heuristic : heuristics) {
        double score = heuristic->evaluate(state, playerId, action);
        totalScore += score;
        
        if (enableLogging) {
            std::cout << "  " << heuristic->getName() << ": " << score << std::endl;
        }
    }
    
    return totalScore;
}

std::unordered_map<BotAction, double> HeuristicsEngine::evaluateAllActions(const GameState& state, const std::string& playerId) const {
    std::unordered_map<BotAction, double> actionScores;
    auto legalActions = state.getLegalActions(playerId);
    
    for (const auto& action : legalActions) {
        actionScores[action] = evaluateAction(state, playerId, action);
    }
    
    return actionScores;
}

std::vector<std::pair<std::string, double>> HeuristicsEngine::getHeuristicContributions(
    const GameState& state, const std::string& playerId, BotAction action) const {
    
    std::vector<std::pair<std::string, double>> contributions;
    
    for (const auto& heuristic : heuristics) {
        double score = heuristic->evaluate(state, playerId, action);
        contributions.emplace_back(heuristic->getName(), score);
    }
    
    return contributions;
}

void HeuristicsEngine::loadBalancedPreset() {
    setHeuristicWeight("PelletDistance", 2.0);
    setHeuristicWeight("PelletDensity", 1.5);
    setHeuristicWeight("ScoreStreak", 1.8);
    setHeuristicWeight("ZookeeperAvoidance", 5.0);
    setHeuristicWeight("ZookeeperPrediction", 3.5);
    setHeuristicWeight("PowerUpCollection", 2.5);
    setHeuristicWeight("PowerUpUsage", 3.0);
    setHeuristicWeight("CenterControl", 0.8);
    setHeuristicWeight("WallAvoidance", 1.2);
    setHeuristicWeight("MovementConsistency", 0.6);
    setHeuristicWeight("TerritoryControl", 1.4);
    setHeuristicWeight("OpponentBlocking", 1.0);
    setHeuristicWeight("Endgame", 2.0);
}

// HeuristicUtils implementation
namespace HeuristicUtils {
    double calculateDistance(const Position& a, const Position& b) {
        return std::sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
    }
    
    double calculateNormalizedDistance(const Position& a, const Position& b, int maxDistance) {
        double distance = a.manhattanDistance(b);
        return std::min(1.0, distance / maxDistance);
    }
    
    std::vector<Position> getPositionsInRadius(const Position& center, int radius, const GameState& state) {
        std::vector<Position> positions;
        
        for (int dx = -radius; dx <= radius; dx++) {
            for (int dy = -radius; dy <= radius; dy++) {
                int x = center.x + dx;
                int y = center.y + dy;
                
                if (state.isValidPosition(x, y) && state.isTraversable(x, y)) {
                    positions.emplace_back(x, y);
                }
            }
        }
        
        return positions;
    }
    
    double calculateAreaControl(const Position& center, int radius, const GameState& state, const std::string& playerId) {
        auto positions = getPositionsInRadius(center, radius, state);
        double controlValue = 0.0;
        
        for (const auto& pos : positions) {
            // Higher value for positions with pellets
            CellContent content = state.getCell(pos.x, pos.y);
            if (content == CellContent::Pellet || content == CellContent::PowerPellet) {
                controlValue += 10.0;
            } else {
                controlValue += 1.0;
            }
            
            // Bonus for positions closer to center
            double distance = center.manhattanDistance(pos);
            controlValue += (radius - distance) / radius * 5.0;
        }
        
        return controlValue;
    }
    
    bool isInDangerZone(const Position& pos, const std::vector<Zookeeper>& zookeepers, int dangerRadius) {
        for (const auto& zk : zookeepers) {
            if (pos.manhattanDistance(zk.position) < dangerRadius) {
                return true;
            }
        }
        return false;
    }
    
    double calculatePelletValue(const GameState& state, const Position& pelletPos, const std::string& playerId) {
        const Animal* animal = state.getAnimal(playerId);
        if (!animal) return 1.0;
        
        double baseValue = 1.0;
        
        // Apply streak multiplier
        baseValue *= animal->scoreStreak;
        
        // Apply power-up multiplier
        if (animal->powerUpDuration > 0 && animal->heldPowerUp == PowerUpType::BigMooseJuice) {
            baseValue *= 3.0;
        }
        
        // Check if it's a power pellet
        if (state.getCell(pelletPos.x, pelletPos.y) == CellContent::PowerPellet) {
            baseValue *= 10.0;
        }
        
        return baseValue;
    }
}