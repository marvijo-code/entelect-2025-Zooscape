#include "MCTSEngine.h"
#include "fmt/core.h" // Added for fmt::println
#include <algorithm>
#include <cmath>
#include <chrono>
#include <future>
#include <iostream>
#include <random>

thread_local std::mt19937 MCTSEngine::rng(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count()));

MCTSEngine::MCTSEngine(double explorationConstant, int maxIterations, int maxSimulationDepth, 
                       int timeLimit, int numThreads)
    : explorationConstant(explorationConstant)
    , maxIterations(maxIterations)
    , maxSimulationDepth(maxSimulationDepth)
    , timeLimit(std::chrono::milliseconds(timeLimit))
    , numThreads(numThreads)
    , shouldStop(false)
    , totalSimulations(0)
    , totalExpansions(0)
    , heuristicsEngine(false) { // Initialize HeuristicsEngine
    
    heuristicsEngine.loadBalancedPreset();
}

MCTSEngine::~MCTSEngine() {
    shouldStop = true;
}

MCTSResult MCTSEngine::findBestAction(const GameState& state, const std::string& playerId) {
    resetStatistics();
    shouldStop = false;
    
    auto rootState = state.clone();
    auto root = std::make_unique<MCTSNode>(std::move(rootState), nullptr, BotAction::Up, playerId);
    
    auto startTime = std::chrono::steady_clock::now();
    
    if (numThreads <= 1) {
        // Single-threaded MCTS
        for (int iteration = 0; iteration < maxIterations && !shouldStop; ++iteration) {
            auto currentTime = std::chrono::steady_clock::now();
            if (currentTime - startTime >= timeLimit) {
                break;
            }
            
            // Selection
            MCTSNode* selectedNode = select(root.get());
            
            // Expansion
            MCTSNode* nodeToSimulate = selectedNode;
            if (!selectedNode->isTerminalNode()) {
                MCTSNode* expandedNode = expand(selectedNode);
                if (expandedNode != selectedNode) { // Check if a new node was created
                    nodeToSimulate = expandedNode;
                    totalExpansions++;
                }
            }
            
            // Simulation
            double reward = simulate(nodeToSimulate->getGameState(), playerId);
            totalSimulations++;
            
            // Backpropagation
            backpropagate(nodeToSimulate, reward);
        }
    } else {
        // Multi-threaded MCTS
        std::vector<std::future<void>> futures;
        
        for (int threadId = 0; threadId < numThreads; ++threadId) {
            futures.push_back(std::async(std::launch::async, 
                [this, &root, &playerId, threadId]() {
                    runParallelMCTS(root.get(), playerId, threadId);
                }));
        }
        
        // Wait for time limit
        std::this_thread::sleep_for(timeLimit);
        shouldStop = true;
        
        // Wait for all threads to complete
        for (auto& future : futures) {
            future.wait();
        }
    }
    
#ifdef ENABLE_MCTS_DEBUG
    // Print final statistics for debugging
    fmt::println("\nTick: {} | Sims: {} | Children: {}", state.tick, totalSimulations.load(), root->getChildren().size());
    fmt::println("{:<12} | {:>10} | {:>15} | {:>15}", "Action", "Visits", "Avg Reward", "UCB1");
    for (const auto& child : root->getChildren()) {
        fmt::println("{:<12} | {:>10} | {:>15.4f} | {:>15.4f}", 
                     static_cast<int>(child->getAction()),
                     child->getVisits(), 
                     child->getAverageReward(),
                     calculateUCB1(child.get(), root.get()));
    }
#endif

    MCTSResult result;
    result.bestAction = BotAction::None;

    MCTSNode* bestChild = nullptr;
    int maxVisits = -1;

    for (const auto& child : root->getChildren()) {
        if (child->getVisits() > maxVisits) {
            maxVisits = child->getVisits();
            bestChild = child.get();
        }
        double avgScore = child->getAverageReward();
        result.allActionStats.push_back({child->getAction(), child->getVisits(), avgScore});
    }

    if (bestChild) {
        result.bestAction = bestChild->getAction();
    } else {
        // Fallback if no children were explored
        auto possibleMoves = state.getLegalActions(playerId);
        if (!possibleMoves.empty()) {
            result.bestAction = possibleMoves[0]; // Or some other default
        }
    }

    return result;
}

MCTSNode* MCTSEngine::select(MCTSNode* root) {
    MCTSNode* current = root;
    
    while (!current->isTerminalNode() && current->isFullyExpandedNode()) {
        constexpr double EPS = 1e-9;
        double bestUCB = -std::numeric_limits<double>::infinity();
        std::vector<MCTSNode*> bestChildren;

        for (const auto& child : current->getChildren()) {
            double ucb = calculateUCB1(child.get(), current);
            if (ucb > bestUCB + EPS) {
                bestUCB = ucb;
                bestChildren.clear();
                bestChildren.push_back(child.get());
            } else if (std::abs(ucb - bestUCB) <= EPS) {
                bestChildren.push_back(child.get());
            }
        }

        if (bestChildren.empty()) {
            break; // Should not happen but safety first
        }

        // Randomised tie-break among equally good children
        std::uniform_int_distribution<size_t> dist(0, bestChildren.size() - 1);
        current = bestChildren[dist(rng)];
    }
    
    return current;
}

MCTSNode* MCTSEngine::expand(MCTSNode* node) {
    if (node->isTerminalNode() || node->isFullyExpandedNode()) {
        return node;
    }
    
    std::lock_guard<std::mutex> lock(treeMutex);
    
    // Double-check after acquiring lock
    if (node->isFullyExpandedNode()) {
        return node;
    }
    
    return node->expand();
}

double MCTSEngine::simulate(const GameState& state, const std::string& playerId) {
    GameState simState = state;
    int depth = 0;
    double cumulativeReward = 0.0;
    double decayFactor = 0.95; // Decay factor for future rewards
    
    while (!simState.isTerminal() && depth < maxSimulationDepth) {
        auto legalActions = simState.getLegalActions(playerId);
        if (legalActions.empty()) {
            break;
        }
        
        const Animal* currentAnimal = simState.getAnimal(playerId);
        if (!currentAnimal) break;
        
        int scoreBeforeAction = currentAnimal->score;
        
        BotAction action = selectSimulationAction(simState, playerId);
        simState.applyAction(playerId, action);

        // Calculate immediate reward for this step
        const Animal* newAnimal = simState.getAnimal(playerId);
        if (newAnimal) {
            int scoreDelta = newAnimal->score - scoreBeforeAction;
            if (scoreDelta > 0) {
                // Immediate reward for pellet collection, decayed by depth
                cumulativeReward += scoreDelta * 50.0 * std::pow(decayFactor, depth);
            }
        }

        // --- Simulate zookeeper movement (greedy one-step towards target) ---
        for (auto& zk : simState.zookeepers) {
            Position nextPos = simState.predictZookeeperPosition(zk, 1);
            zk.position = nextPos;
            // Capture check – if zookeeper ends on the same cell as the player, mark caught
            Animal* myAnimal = simState.getAnimal(playerId);
            if (myAnimal && myAnimal->position == zk.position) {
                myAnimal->isCaught = true;
            }
        }
        
        // Terminate rollout early if captured, but apply penalty
        if (simState.isPlayerCaught(playerId)) {
            cumulativeReward -= 2000.0 * std::pow(decayFactor, depth);
            break;
        }

        depth++;
    }
    
    // Combine cumulative step rewards with final state evaluation
    double terminalReward = evaluateTerminalState(simState, playerId);
    return cumulativeReward + terminalReward * std::pow(decayFactor, depth);
}

void MCTSEngine::backpropagate(MCTSNode* node, double reward) {
    MCTSNode* current = node;
    
    while (current != nullptr) {
        current->update(reward);
        current = current->getParent();
        
        // Alternate reward for opponent modeling (if needed)
        // reward = -reward;
    }
}

double MCTSEngine::calculateUCB1(const MCTSNode* node, const MCTSNode* parent) const {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity(); // Prioritize unvisited nodes
    }

    double exploitation = node->getAverageReward();
    double exploration = explorationConstant * 
                        std::sqrt(std::log(static_cast<double>(parent->getVisits())) / node->getVisits());
    
    // Small first-play urgency bonus to help break ties for new nodes
    double firstPlayUrgency = 0.0;
    if (node->getVisits() < 3) {
        firstPlayUrgency = 100.0 / (node->getVisits() + 1);
    }

    return exploitation + exploration + firstPlayUrgency;
}

double MCTSEngine::calculateRAVE(const MCTSNode* node) const {
    // RAVE implementation would go here
    // For now, return 0 as it's not fully implemented
    return 0.0;
}

bool MCTSEngine::shouldExpandNode(const MCTSNode* node) const {
    // Progressive widening: expand when visits^alpha > children
    double alpha = 0.5;
    int visits = node->getVisits();
    int children = static_cast<int>(node->getChildren().size());
    
    return std::pow(visits, alpha) > children;
}

BotAction MCTSEngine::selectSimulationAction(const GameState& state, const std::string& playerId) {
    auto legalActions = state.getLegalActions(playerId);
    if (legalActions.empty()) {
        return BotAction::Up;
    }
    
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) {
        // Random fallback
        std::uniform_int_distribution<size_t> dist(0, legalActions.size() - 1);
        return legalActions[dist(rng)];
    }
    
    // Fast greedy policy for simulation - focus on immediate goals
    BotAction bestAction = legalActions[0];
    double bestScore = -std::numeric_limits<double>::infinity();
    
    for (const auto& action : legalActions) {
        double score = 0.0;
        
        Position newPos = animal->position;
        switch (action) {
            case BotAction::Up: newPos.y--; break;
            case BotAction::Down: newPos.y++; break;
            case BotAction::Left: newPos.x--; break;
            case BotAction::Right: newPos.x++; break;
            case BotAction::UseItem:
                // Favor using power-ups when beneficial
                if (animal->heldPowerUp == PowerUpType::Scavenger) {
                    score += 500.0; // High value for scavenger
                } else if (animal->heldPowerUp == PowerUpType::ChameleonCloak && 
                          state.getZookeeperThreat(animal->position) > 5.0) {
                    score += 400.0; // Use cloak when threatened
                }
                continue;
        }
        
        if (!state.isTraversable(newPos.x, newPos.y)) {
            score = -1000.0; // Invalid move
        } else {
            // Check what's at the destination
            CellContent cellContent = state.getCell(newPos.x, newPos.y);
            switch (cellContent) {
                case CellContent::Pellet:
                    score += 300.0 * animal->scoreStreak; // Value pellets highly
                    break;
                case CellContent::PowerPellet:
                    score += 600.0 * animal->scoreStreak; // Power pellets even more
                    break;
                case CellContent::Scavenger:
                    score += 250.0; // Valuable power-up
                    break;
                case CellContent::ChameleonCloak:
                    score += 200.0 + state.getZookeeperThreat(newPos) * 10.0; // More valuable when threatened
                    break;
                case CellContent::BigMooseJuice:
                    score += 150.0;
                    break;
                default:
                    break;
            }
            
            // Distance to nearest pellet
            int distToPellet = state.distanceToNearestPellet(newPos);
            if (distToPellet >= 0) {
                score += 50.0 / (distToPellet + 1); // Closer is better
            }
            
            // Avoid zookeepers
            double threat = state.getZookeeperThreat(newPos);
            score -= threat * 20.0;
            
            // Slight randomization to avoid deterministic patterns
            std::uniform_real_distribution<double> noise(-5.0, 5.0);
            score += noise(rng);
        }
        
        if (score > bestScore) {
            bestScore = score;
            bestAction = action;
        }
    }
    
    return bestAction;
}

double MCTSEngine::evaluateTerminalState(const GameState& state, const std::string& playerId) {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) {
        return 0.0; // Should not happen
    }

    // Immediate failure conditions
    if (state.isPlayerCaught(playerId)) {
        return -10000.0; // Severe penalty for being caught
    }

    // 1. Pellet Score (Primary Reward) - Weight heavily as it's the main objective
    double pelletScore = static_cast<double>(animal->score) * 100.0; // Increased weight

    // 2. Distance to nearest pellet (encourage moving towards pellets)
    int distToPellet = state.distanceToNearestPellet(animal->position);
    double distanceReward = 0.0;
    if (distToPellet >= 0) {
        // Exponential decay for distance - being close is much more valuable
        distanceReward = 50.0 * std::exp(-0.3 * distToPellet);
    }

    // 3. Score streak bonus - maintaining streaks is critical
    double streakBonus = animal->scoreStreak * 25.0;
    
    // 4. Immediate pellet collection bonus
    double immediateBonus = 0.0;
    if (animal->ticksSinceLastPellet == 0) {
        immediateBonus = 200.0 * animal->scoreStreak; // Bonus for fresh pellet collection
    }

    // 5. Streak preservation penalty - avoid moves that reset streak
    double streakPenalty = 0.0;
    if (animal->ticksSinceLastPellet >= 2) {
        streakPenalty = 100.0 * animal->scoreStreak; // Penalty proportional to streak risk
    }

    // 6. Threat penalty (avoid zookeepers) - but don't make it overwhelming
    double threatPenalty = state.getZookeeperThreat(animal->position) * 15.0;

    // 7. Power-up value
    double powerUpValue = 0.0;
    if (animal->heldPowerUp != PowerUpType::None) {
        switch (animal->heldPowerUp) {
            case PowerUpType::Scavenger:
                powerUpValue = 80.0; // Very valuable for pellet collection
                break;
            case PowerUpType::ChameleonCloak:
                powerUpValue = 60.0 + threatPenalty; // More valuable when threatened
                break;
            case PowerUpType::BigMooseJuice:
                powerUpValue = 50.0;
                break;
            default:
                break;
        }
    }
    if (animal->powerUpDuration > 0) {
        powerUpValue += animal->powerUpDuration * 5.0; // Active power-up bonus
    }

    // 8. Position quality - favor center and avoid corners
    double positionBonus = 0.0;
    if (state.getWidth() > 0 && state.getHeight() > 0) {
        double centerX = state.getWidth() / 2.0;
        double centerY = state.getHeight() / 2.0;
        double distFromCenter = std::sqrt(std::pow(animal->position.x - centerX, 2) + 
                                         std::pow(animal->position.y - centerY, 2));
        positionBonus = std::max(0.0, 20.0 - distFromCenter * 2.0);
    }

    // Final score calculation - no tanh compression to maintain discrimination
    double finalScore = pelletScore + distanceReward + streakBonus + immediateBonus 
                       - streakPenalty - threatPenalty + powerUpValue + positionBonus;

    return finalScore;
}

void MCTSEngine::runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId) {
        std::mt19937 localRng(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count() + threadId));
    
    while (!shouldStop.load()) {
        // Selection
        MCTSNode* selectedNode = select(root);
        
        // Expansion
        MCTSNode* nodeToSimulate = selectedNode;
        if (!selectedNode->isTerminalNode()) {
            if (selectedNode->tryLockExpansion()) {
                if (!selectedNode->isFullyExpandedNode()) { // Double check after lock
                    MCTSNode* expandedNode = selectedNode->expand();
                    if (expandedNode != selectedNode) { // Check if a *new* node was created
                        nodeToSimulate = expandedNode;
                        totalExpansions++;
                    }
                }
                selectedNode->unlockExpansion();
            }
        }
        
        // Simulation
        double reward = simulate(nodeToSimulate->getGameState(), playerId);
        totalSimulations++;
        
        // Backpropagation
        backpropagate(nodeToSimulate, reward);
    }
}

void MCTSEngine::enableProgressiveWidening(bool enable) {
    // Implementation for enabling/disabling progressive widening
}

void MCTSEngine::enableRAVE(bool enable) {
    // Implementation for enabling/disabling RAVE
}

void MCTSEngine::setHeuristicWeight(double weight) {
    // Implementation for setting heuristic weight in simulations
}

// UCB1-Tuned implementation
double UCB1Tuned::calculate(const MCTSNode* node, const MCTSNode* parent, double explorationConstant) {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    double exploitation = node->getAverageReward();
    double logParentVisits = std::log(parent->getVisits());
    double nodeVisits = node->getVisits();
    
    double variance = node->getRewardVariance();
    double varianceBound = variance + std::sqrt(2 * logParentVisits / nodeVisits);
    double exploration = explorationConstant * std::sqrt(logParentVisits / nodeVisits * 
                                                        std::min(CONFIDENCE_BOUND, varianceBound));
    
    return exploitation + exploration;
}

// RAVE implementation
void RAVE::updateActionValue(BotAction action, double reward) {
    auto& [totalReward, visits] = actionValues[action];
    totalReward += reward;
    visits++;
}

double RAVE::getActionValue(BotAction action) const {
    auto it = actionValues.find(action);
    if (it != actionValues.end() && it->second.second > 0) {
        return it->second.first / it->second.second;
    }
    return 0.0;
}

double RAVE::calculateRAVEValue(const MCTSNode* node) const {
    // Combine MCTS value with RAVE value using beta parameter
    double mctsValue = node->getAverageReward();
    double raveValue = getActionValue(node->getAction());
    
    int visits = node->getVisits();
    double betaValue = visits / (visits + beta * visits + beta);
    
    return (1 - betaValue) * raveValue + betaValue * mctsValue;
}

// Progressive Widening implementation
bool ProgressiveWidening::shouldExpand(int visits, int children) const {
    return std::pow(visits, alpha) > children + threshold;
}

int ProgressiveWidening::getMaxChildren(int visits) const {
    return static_cast<int>(std::pow(visits, alpha));
}