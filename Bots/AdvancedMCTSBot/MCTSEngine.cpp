#include "MCTSEngine.h"
#include "fmt/core.h" // Added for fmt::println
#include <algorithm>
#include <cmath>
#include <chrono>
#include <future>
#include <iostream>
#include <random>
#include <sstream>
#include <functional>

thread_local std::mt19937 MCTSEngine::rng(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count()));

// Modern MCTS Enhancement Implementations

// TranspositionTable Implementation
std::shared_ptr<MCTSNode> TranspositionTable::lookup(const std::string& stateHash) {
    std::lock_guard<std::mutex> lock(tableMutex);
    auto it = table.find(stateHash);
    if (it != table.end()) {
        it->second.lastAccessed = std::chrono::steady_clock::now();
        return it->second.node.lock(); // May return nullptr if expired
    }
    return nullptr;
}

void TranspositionTable::store(const std::string& stateHash, std::shared_ptr<MCTSNode> node) {
    std::lock_guard<std::mutex> lock(tableMutex);
    if (table.size() >= maxSize) {
        cleanup();
    }
    table[stateHash] = {stateHash, node, node->getVisits(), node->getAverageReward(), 
                       std::chrono::steady_clock::now()};
}

void TranspositionTable::cleanup() {
    auto now = std::chrono::steady_clock::now();
    auto it = table.begin();
    while (it != table.end()) {
        if (it->second.node.expired() || 
            std::chrono::duration_cast<std::chrono::minutes>(now - it->second.lastAccessed).count() > 5) {
            it = table.erase(it);
        } else {
            ++it;
        }
    }
}

// VirtualLoss Implementation
void VirtualLoss::addVirtualLoss(MCTSNode* node) {
    std::lock_guard<std::mutex> lock(lossMapMutex);
    auto& atomicValue = virtualLosses[node];
    double currentValue = atomicValue.load();
    atomicValue.store(currentValue + virtualLossValue);
}

void VirtualLoss::removeVirtualLoss(MCTSNode* node) {
    std::lock_guard<std::mutex> lock(lossMapMutex);
    auto it = virtualLosses.find(node);
    if (it != virtualLosses.end()) {
        double currentValue = it->second.load();
        double newValue = currentValue - virtualLossValue;
        if (newValue <= 0) {
            virtualLosses.erase(it);
        } else {
            it->second.store(newValue);
        }
    }
}

double VirtualLoss::getVirtualLoss(MCTSNode* node) const {
    std::lock_guard<std::mutex> lock(lossMapMutex);
    auto it = virtualLosses.find(node);
    return it != virtualLosses.end() ? it->second.load() : 0.0;
}

// AMAF Implementation
void AMAF::updateAMAF(const std::vector<BotAction>& sequence, double finalReward) {
    std::lock_guard<std::mutex> lock(statsMutex);
    for (BotAction action : sequence) {
        auto& stats = globalStats[action];
        double currentReward = stats.totalReward.load();
        stats.totalReward.store(currentReward + finalReward);
        int currentVisits = stats.visits.load();
        stats.visits.store(currentVisits + 1);
    }
}

double AMAF::getAMAFValue(BotAction action) const {
    std::lock_guard<std::mutex> lock(statsMutex);
    auto it = globalStats.find(action);
    if (it != globalStats.end() && it->second.visits > 0) {
        return it->second.totalReward / it->second.visits;
    }
    return 0.0;
}

double AMAF::combinedValue(double mctsValue, BotAction action, int mctsVisits) const {
    double amafValue = getAMAFValue(action);
    if (mctsVisits == 0) return amafValue;
    
    double combinationWeight = mctsVisits / (mctsVisits + beta * mctsVisits + beta);
    return combinationWeight * mctsValue + (1 - combinationWeight) * amafValue;
}

// Enhanced Bandit Algorithm Implementations
double EnhancedUCB1::calculateValue(const MCTSNode* node, const MCTSNode* parent) const {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    double exploitation = node->getAverageReward();
    double exploration = explorationConstant * std::sqrt(std::log(parent->getVisits()) / node->getVisits());
    
    // Progressive bias based on domain knowledge
    double bias = 0.0;
    if (progressiveBiasWeight > 0.0) {
        // Add heuristic-based bias that decreases with visits
        const GameState& state = node->getGameState();
        // Simple heuristic: prefer actions that lead to better positions
        bias = progressiveBiasWeight / (1.0 + node->getVisits() * 0.1);
    }
    
    return exploitation + exploration + bias;
}

double UCB_V::calculateValue(const MCTSNode* node, const MCTSNode* parent) const {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    double exploitation = node->getAverageReward();
    double variance = node->getRewardVariance();
    double logParentVisits = std::log(parent->getVisits());
    double nodeVisits = node->getVisits();
    
    double exploration = explorationConstant * std::sqrt(logParentVisits / nodeVisits);
    double varianceTerm = std::sqrt(variance * logParentVisits / nodeVisits);
    
    return exploitation + exploration + varianceScale * varianceTerm;
}

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
    , heuristicsEngine(false)
    , useTranspositionTable(true)
    , useVirtualLoss(true)
    , useAMAF(true)
    , useProgressiveWidening(false)
    , useRAVE(true)
    , heuristicWeight(0.5) {
    
    heuristicsEngine.loadBalancedPreset();
    
    // Initialize modern MCTS enhancements
    transpositionTable = std::make_unique<TranspositionTable>(50000);
    virtualLoss = std::make_unique<VirtualLoss>(5.0);
    amaf = std::make_unique<AMAF>(0.3);
    banditAlgorithm = std::make_unique<UCB_V>(1.0, 0.25); // Default to UCB-V
}

MCTSEngine::~MCTSEngine() {
    shouldStop = true;
}

std::string MCTSEngine::hashGameState(const GameState& state, const std::string& playerId) const {
    std::ostringstream oss;
    
    // Hash key game state components
    oss << "tick:" << state.tick << "|";
    oss << "player:" << playerId << "|";
    
    // Player position and state
    const Animal* animal = state.getAnimal(playerId);
    if (animal) {
        oss << "pos:" << animal->position.x << "," << animal->position.y << "|";
        oss << "score:" << animal->score << "|";
        oss << "streak:" << animal->scoreStreak << "|";
        oss << "lastPellet:" << animal->ticksSinceLastPellet << "|";
        oss << "powerUp:" << static_cast<int>(animal->heldPowerUp) << "|";
        oss << "powerUpDur:" << animal->powerUpDuration << "|";
    }
    
    // Pellet board (simplified - only nearby pellets)
    if (animal) {
        for (int dx = -3; dx <= 3; ++dx) {
            for (int dy = -3; dy <= 3; ++dy) {
                int x = animal->position.x + dx;
                int y = animal->position.y + dy;
                if (x >= 0 && x < state.getWidth() && y >= 0 && y < state.getHeight()) {
                    if (state.pelletBoard.get(x, y)) {
                        oss << "p:" << x << "," << y << "|";
                    }
                }
            }
        }
    }
    
    // Zookeeper positions (simplified)
    for (const auto& zk : state.zookeepers) {
        oss << "zk:" << zk.position.x << "," << zk.position.y << "|";
    }
    
    return oss.str();
}

void MCTSEngine::initializeMoveOrdering(const GameState& state, const std::string& playerId) {
    moveOrdering = {BotAction::Up, BotAction::Down, BotAction::Left, BotAction::Right};
    
    // Order moves based on simple heuristics
    const Animal* animal = state.getAnimal(playerId);
    if (animal) {
        // Find nearest pellet direction
        int nearestPelletDist = std::numeric_limits<int>::max();
        Position nearestPelletPos{-1, -1};
        
        for (int y = 0; y < state.getHeight(); ++y) {
            for (int x = 0; x < state.getWidth(); ++x) {
                if (state.pelletBoard.get(x, y)) {
                    int dist = std::abs(animal->position.x - x) + std::abs(animal->position.y - y);
                    if (dist < nearestPelletDist) {
                        nearestPelletDist = dist;
                        nearestPelletPos = {x, y};
                    }
                }
            }
        }
        
        if (nearestPelletPos.x >= 0) {
            // Reorder moves to prioritize directions toward nearest pellet
            std::sort(moveOrdering.begin(), moveOrdering.end(), 
                [this, animal, nearestPelletPos](BotAction a, BotAction b) {
                    Position newPosA = this->getNewPosition(animal->position, a);
                    Position newPosB = this->getNewPosition(animal->position, b);
                    
                    int distA = std::abs(newPosA.x - nearestPelletPos.x) + std::abs(newPosA.y - nearestPelletPos.y);
                    int distB = std::abs(newPosB.x - nearestPelletPos.x) + std::abs(newPosB.y - nearestPelletPos.y);
                    
                    return distA < distB;
                });
        }
    }
}

std::vector<BotAction> MCTSEngine::getOrderedMoves(const GameState& state, const std::string& playerId) {
    auto legalMoves = state.getLegalActions(playerId);
    std::vector<BotAction> orderedMoves;
    
    // First add moves in our preferred order
    for (BotAction action : moveOrdering) {
        if (std::find(legalMoves.begin(), legalMoves.end(), action) != legalMoves.end()) {
            orderedMoves.push_back(action);
        }
    }
    
    // Add any remaining legal moves
    for (BotAction action : legalMoves) {
        if (std::find(orderedMoves.begin(), orderedMoves.end(), action) == orderedMoves.end()) {
            orderedMoves.push_back(action);
        }
    }
    
    return orderedMoves;
}

bool MCTSEngine::shouldPruneMove(BotAction action, const GameState& state, const std::string& playerId) {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) return false;
    
    Position newPos = this->getNewPosition(animal->position, action);
    
    // Prune moves that lead directly into zookeepers
    for (const auto& zk : state.zookeepers) {
        if (newPos == zk.position) {
            return true;
        }
    }
    
    // Prune moves that lead to dead ends when being chased
    double threat = state.getZookeeperThreat(newPos);
    if (threat > 0.8) {
        // Simple dead end check - if it's out of bounds or into a wall
        if (newPos.x < 0 || newPos.x >= state.getWidth() || 
            newPos.y < 0 || newPos.y >= state.getHeight() ||
            !state.isTraversable(newPos.x, newPos.y)) {
            return true;
        }
    }
    
    return false;
}

Position MCTSEngine::getNewPosition(const Position& currentPos, BotAction action) const {
    Position newPos = currentPos;
    switch (action) {
        case BotAction::Up: newPos.y--; break;
        case BotAction::Down: newPos.y++; break;
        case BotAction::Left: newPos.x--; break;
        case BotAction::Right: newPos.x++; break;
        default: break;
    }
    return newPos;
}

bool MCTSEngine::shouldContinueSearch(std::chrono::steady_clock::time_point startTime) const {
    auto elapsed = std::chrono::steady_clock::now() - startTime;
    return elapsed < timeLimit * 0.95; // Use 95% of time limit for safety
}

MCTSResult MCTSEngine::findBestAction(const GameState& state, const std::string& playerId) {
    resetStatistics();
    shouldStop = false;
    
    initializeMoveOrdering(state, playerId);
    
    auto rootState = state.clone();
    auto root = std::make_unique<MCTSNode>(std::move(rootState), nullptr, BotAction::Up, playerId);
    
    auto startTime = std::chrono::steady_clock::now();
    
    if (numThreads <= 1) {
        // Single-threaded MCTS with modern enhancements
        for (int iteration = 0; iteration < maxIterations && !shouldStop; ++iteration) {
            if (!shouldContinueSearch(startTime)) {
                break;
            }
            
            // Selection
            MCTSNode* selectedNode = select(root.get());
            
            // Expansion
            MCTSNode* nodeToSimulate = selectedNode;
            if (!selectedNode->isTerminalNode()) {
                MCTSNode* expandedNode = expand(selectedNode);
                if (expandedNode != selectedNode) {
                    nodeToSimulate = expandedNode;
                    totalExpansions++;
                }
            }
            
            // Simulation with action sequence tracking
            std::vector<BotAction> actionSequence;
            double reward = simulate(nodeToSimulate->getGameState(), playerId, actionSequence);
            totalSimulations++;
            
            // Backpropagation with AMAF update
            backpropagate(nodeToSimulate, reward, actionSequence);
        }
    } else {
        // Multi-threaded MCTS with virtual loss
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
    // Print enhanced debugging information
    fmt::println("\nAdvanced MCTS Statistics:");
    fmt::println("Tick: {} | Sims: {} | Children: {} | Algorithm: {}", 
                 state.tick, totalSimulations.load(), root->getChildren().size(),
                 banditAlgorithm ? banditAlgorithm->getName() : "Standard UCB1");
    if (useTranspositionTable) {
        fmt::println("Transposition Table Size: {}", transpositionTable->size());
    }
    fmt::println("{:<12} | {:>10} | {:>15} | {:>15} | {:>15}", 
                 "Action", "Visits", "Avg Reward", "UCB Value", "AMAF Value");
    
    for (const auto& child : root->getChildren()) {
        double ucbValue = banditAlgorithm ? 
            banditAlgorithm->calculateValue(child.get(), root.get()) :
            calculateUCB1(child.get(), root.get());
        double amafValue = useAMAF ? amaf->getAMAFValue(child->getAction()) : 0.0;
        
        fmt::println("{:<12} | {:>10} | {:>15.4f} | {:>15.4f} | {:>15.4f}", 
                     static_cast<int>(child->getAction()),
                     child->getVisits(), 
                     child->getAverageReward(),
                     ucbValue,
                     amafValue);
    }
#endif

    MCTSResult result;
    result.bestAction = BotAction::None;

    // Select the child with the highest visit count (robust measure)
    MCTSNode* bestChild = nullptr;
    int bestVisits = -1;

    for (const auto& child : root->getChildren()) {
        int visits = child->getVisits();
        if (visits > bestVisits) {
            bestVisits = visits;
            bestChild = child.get();
        } else if (visits == bestVisits && bestChild != nullptr) {
            // Tie-break: higher average reward
            if (child->getAverageReward() > bestChild->getAverageReward()) {
                bestChild = child.get();
            }
        }

        // Collect stats for caller
        double avgScore = child->getAverageReward();
        result.allActionStats.push_back({child->getAction(), visits, avgScore});
    }

    if (bestChild) {
        result.bestAction = bestChild->getAction();
    } else {
        // Fallback if no children were explored (rare)
        auto possibleMoves = state.getLegalActions(playerId);
        if (!possibleMoves.empty()) {
            result.bestAction = possibleMoves[0];
        }
    }

    return result;
}

MCTSNode* MCTSEngine::select(MCTSNode* root) {
    MCTSNode* current = root;
    
    while (!current->isTerminalNode() && current->isFullyExpandedNode()) {
        constexpr double EPS = 1e-9;
        double bestValue = -std::numeric_limits<double>::infinity();
        std::vector<MCTSNode*> bestChildren;

        for (const auto& child : current->getChildren()) {
            double value;
            
            if (banditAlgorithm) {
                // Use the configured bandit algorithm
                value = banditAlgorithm->calculateValue(child.get(), current);
            } else {
                // Fallback to standard UCB1
                value = calculateUCB1(child.get(), current);
            }
            
            // Apply AMAF if enabled
            if (useAMAF) {
                double mctsValue = child->getAverageReward();
                double combinedValue = amaf->combinedValue(mctsValue, child->getAction(), child->getVisits());
                value = 0.7 * value + 0.3 * combinedValue; // Weighted combination
            }
            
            // Apply virtual loss if enabled (for multi-threading)
            if (useVirtualLoss && numThreads > 1) {
                double virtualLossValue = virtualLoss->getVirtualLoss(child.get());
                value -= virtualLossValue;
            }
            
            if (value > bestValue + EPS) {
                bestValue = value;
                bestChildren.clear();
                bestChildren.push_back(child.get());
            } else if (std::abs(value - bestValue) <= EPS) {
                bestChildren.push_back(child.get());
            }
        }

        if (bestChildren.empty()) {
            break; // Should not happen but safety first
        }

        // Randomized tie-break among equally good children
        std::uniform_int_distribution<size_t> dist(0, bestChildren.size() - 1);
        current = bestChildren[dist(rng)];
        
        // Apply virtual loss to selected node
        if (useVirtualLoss && numThreads > 1) {
            virtualLoss->addVirtualLoss(current);
        }
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
    
    // Check transposition table first
    if (useTranspositionTable) {
        std::string stateHash = hashGameState(node->getGameState(), node->getPlayerId());
        auto existingNode = transpositionTable->lookup(stateHash);
        if (existingNode && existingNode.get() != node) {
            // Found an existing equivalent state - merge statistics
            // Simple merging: combine visit counts and average rewards
            int existingVisits = existingNode->getVisits();
            double existingReward = existingNode->getAverageReward();
            int currentVisits = node->getVisits();
            double currentReward = node->getAverageReward();
            
            if (existingVisits > 0 && currentVisits > 0) {
                // Weighted average of rewards
                double combinedReward = (existingReward * existingVisits + currentReward * currentVisits) / 
                                       (existingVisits + currentVisits);
                // Update the existing node with combined statistics
                existingNode->update(combinedReward * (existingVisits + currentVisits) - existingNode->getTotalReward());
            }
            
            return existingNode.get();
        }
    }
    
    // Use the existing expand method
    MCTSNode* expandedNode = node->expand();
    
    // Store in transposition table if new node was created
    if (expandedNode && expandedNode != node && useTranspositionTable) {
        std::string stateHash = hashGameState(expandedNode->getGameState(), expandedNode->getPlayerId());
        // Create a shared_ptr wrapper for the transposition table
        // Note: This is a simplified implementation - proper integration would require
        // redesigning the node ownership model
        std::shared_ptr<MCTSNode> sharedNode(expandedNode, [](MCTSNode*) {
            // Custom deleter that does nothing - the unique_ptr in the tree will handle deletion
        });
        transpositionTable->store(stateHash, sharedNode);
    }
    
    return expandedNode;
}

double MCTSEngine::simulate(const GameState& state, const std::string& playerId, std::vector<BotAction>& actionSequence) {
    GameState simState = state;
    int depth = 0;
    double cumulativeReward = 0.0;
    double decayFactor = 0.95; // Decay factor for future rewards
    
    // Cycle detection: track visited state hashes
    std::unordered_set<std::string> visitedStates;
    int cycleDetectionPenalty = 0;
    
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

        // Cycle detection: check if we've seen this state before
        std::string stateHash = hashGameState(simState, playerId);
        if (visitedStates.find(stateHash) != visitedStates.end()) {
            // Apply moderate penalty for revisiting state but continue rollout
            cumulativeReward -= 100.0 * std::pow(decayFactor, depth);
            cycleDetectionPenalty++;
            if (cycleDetectionPenalty > 3) break; // Only terminate after multiple cycles
        }
        visitedStates.insert(stateHash);

        // Calculate immediate reward for this step
        const Animal* newAnimal = simState.getAnimal(playerId);
        if (newAnimal) {
            int scoreDelta = newAnimal->score - scoreBeforeAction;
            if (scoreDelta > 0) {
                // Scaled immediate reward for pellet collection, with streak multiplier
                double pelletReward = scoreDelta * 100.0 * std::max(1, newAnimal->scoreStreak);
                cumulativeReward += pelletReward * std::pow(decayFactor, depth);
            }
            
            // Exploration reward for visiting new cells
            if (simState.visitedCells.find(newAnimal->position) == simState.visitedCells.end()) {
                double explorationReward = 20.0; // Increased reward for exploration
                cumulativeReward += explorationReward * std::pow(decayFactor, depth);
                simState.visitedCells.insert(newAnimal->position);
            } else {
                // Penalty for revisiting cells
                double revisitPenalty = 10.0;
                cumulativeReward -= revisitPenalty * std::pow(decayFactor, depth);
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
        
        // Apply penalty if captured but continue rollout to learn recovery
        if (simState.isPlayerCaught(playerId)) {
            cumulativeReward -= 500.0 * std::pow(decayFactor, depth);
            break; // Still terminate as capture is terminal
        }

        depth++;
        actionSequence.push_back(action);
    }
    
    // Combine cumulative step rewards with final state evaluation
    double terminalReward = evaluateTerminalState(simState, playerId);
    
    // Apply additional penalty for cycle detection
    double cyclePenalty = cycleDetectionPenalty * 1000.0;
    
    return cumulativeReward + terminalReward * std::pow(decayFactor, depth) - cyclePenalty;
}

void MCTSEngine::backpropagate(MCTSNode* node, double reward, const std::vector<BotAction>& actionSequence) {
    MCTSNode* current = node;
    
    while (current != nullptr) {
        current->update(reward);
        current = current->getParent();
        
        // Alternate reward for opponent modeling (if needed)
        // reward = -reward;
    }

    // Update AMAF
    if (useAMAF) {
        amaf->updateAMAF(actionSequence, reward);
    }
}

double MCTSEngine::calculateUCB1(const MCTSNode* node, const MCTSNode* parent) const {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity(); // Prioritize unvisited nodes
    }

    // Enhanced UCB1 with strong progressive bias toward pellet collection
    const GameState& childState = node->getGameState();
    const Animal* childAnimal = childState.getAnimal(childState.myAnimalId);
    double heuristicBias = 0.0;
    if (childAnimal) {
        int dist = childState.distanceToNearestPellet(childAnimal->position);
        if (dist >= 0) {
            if (dist == 0) {
                heuristicBias = 10.0; // HUGE bias for moves that collect pellets
            } else if (dist == 1) {
                heuristicBias = 5.0; // Strong bias for moves toward pellets
            } else {
                heuristicBias = 2.0 / dist; // Good bias for getting closer
            }
        }
        
        // Additional bias for maintaining streaks
        if (childAnimal->ticksSinceLastPellet >= 3) {
            heuristicBias *= 2.0; // Double bias when streak is at risk
        }
        
        // Check if move leads to immediate pellet collection
        CellContent cellAtPos = childState.getCell(childAnimal->position.x, childAnimal->position.y);
        if (cellAtPos == CellContent::Pellet) {
            heuristicBias += 15.0; // Massive bias for pellet collection moves
        } else if (cellAtPos == CellContent::PowerPellet) {
            heuristicBias += 25.0; // Even bigger bias for power pellets
        }
    }
    // Progressive bias that decays more slowly for important moves
    const double biasWeight = 8.0; // Much stronger bias weight
    double progressiveBias = biasWeight * heuristicBias / (1.0 + std::pow(node->getVisits(), 0.5));

    double exploitation = node->getAverageReward();
    double exploration = explorationConstant * std::sqrt(std::log(parent->getVisits()) / node->getVisits());
    
    return exploitation + exploration + progressiveBias;
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
        std::uniform_int_distribution<size_t> dist(0, legalActions.size() - 1);
        return legalActions[dist(rng)];
    }
    
    // Simplified pellet-focused simulation policy
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
                // Simple power-up usage
                if (animal->heldPowerUp == PowerUpType::Scavenger) {
                    return BotAction::UseItem; // Always use scavenger
                }
                score += 50.0; // Moderate bonus for other power-ups
                break;
        }
        
        if (!state.isTraversable(newPos.x, newPos.y)) {
            continue; // Skip invalid moves
        }
        
        // Simple zookeeper avoidance - avoid if very close
        double threat = state.getZookeeperThreat(newPos);
        if (threat > 5.0) {
            score -= 200.0; // Penalty for high threat
        }
        
        // Strong preference for pellets
        CellContent cellContent = state.getCell(newPos.x, newPos.y);
        if (cellContent == CellContent::Pellet) {
            score += 100.0;
        } else if (cellContent == CellContent::PowerPellet) {
            score += 200.0;
        }
        
        // Move toward nearest pellet
        int distToPellet = state.distanceToNearestPellet(newPos);
        if (distToPellet >= 0 && distToPellet < 10) {
            score += 20.0 / (1.0 + distToPellet);
        }
        
        // Small random component for exploration
        std::uniform_real_distribution<double> noise(-5.0, 5.0);
        score += noise(rng);
        
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

    // Immediate failure conditions - high penalty
    if (state.isPlayerCaught(playerId)) {
        return -5000.0; // Avoid being caught
    }

    // 1. Pellet Score (Primary Reward) - This is the most important factor
    double pelletScore = static_cast<double>(animal->score) * 50.0; // High weight for score

    // 2. Distance to nearest pellet - incentive to get close
    int distToPellet = state.distanceToNearestPellet(animal->position);
    double distanceReward = 0.0;
    if (distToPellet >= 0) {
        if (distToPellet == 0) {
            distanceReward = 100.0; // Good bonus for being on a pellet
        } else if (distToPellet == 1) {
            distanceReward = 40.0; // Close is good
        } else {
            distanceReward = 20.0 / distToPellet; // Inverse distance reward
        }
    }

    // 3. Score streak bonus - increasingly valuable
    double streakBonus = animal->scoreStreak * animal->scoreStreak * 5.0;
    
    // 4. Immediate pellet collection bonus - good reward
    double immediateBonus = 0.0;
    if (animal->ticksSinceLastPellet == 0) {
        immediateBonus = 150.0 * std::max(1, animal->scoreStreak); // Good bonus for fresh collection
    }

    // 5. Streak preservation penalty - penalty for risking streaks
    double streakPenalty = 0.0;
    if (animal->ticksSinceLastPellet >= 3) {
        streakPenalty = 80.0 * animal->scoreStreak; // Penalty for risky streak behavior
    } else if (animal->ticksSinceLastPellet >= 2) {
        streakPenalty = 30.0 * animal->scoreStreak; // Small penalty
    }

    // 6. Threat penalty - zookeeper avoidance
    double threatPenalty = 0.0;
    double currentThreat = state.getZookeeperThreat(animal->position);
    if (currentThreat > 8.0) {
        threatPenalty = 800.0; // High penalty for extreme danger
    } else if (currentThreat > 5.0) {
        threatPenalty = 300.0; // Medium penalty for high danger
    } else if (currentThreat > 2.0) {
        threatPenalty = 80.0 * currentThreat; // Scaled penalty
    } else {
        threatPenalty = 5.0 * currentThreat; // Small penalty for low threat
    }

    // 7. Power-up value - valuable
    double powerUpValue = 0.0;
    if (animal->heldPowerUp != PowerUpType::None) {
        switch (animal->heldPowerUp) {
            case PowerUpType::Scavenger:
                powerUpValue = 50.0; // Valuable for pellet collection
                break;
            case PowerUpType::ChameleonCloak:
                powerUpValue = 30.0 + currentThreat * 2.0; // More valuable when threatened
                break;
            case PowerUpType::BigMooseJuice:
                powerUpValue = 20.0 + currentThreat * 1.0; // More valuable when threatened
                break;
            default:
                break;
        }
    }
    if (animal->powerUpDuration > 0) {
        powerUpValue += animal->powerUpDuration * 5.0; // Active power-up bonus
    }

    // 8. Check proximity to power-ups
    double powerUpProximity = 0.0;
    for (int dx = -3; dx <= 3; ++dx) {
        for (int dy = -3; dy <= 3; ++dy) {
            int x = animal->position.x + dx;
            int y = animal->position.y + dy;
            if (x >= 0 && x < state.getWidth() && y >= 0 && y < state.getHeight()) {
                CellContent content = state.getCell(x, y);
                int distance = std::abs(dx) + std::abs(dy);
                if (distance > 0) {
                    if (content == CellContent::Scavenger) {
                        powerUpProximity += 20.0 / distance;
                    } else if (content == CellContent::ChameleonCloak && currentThreat > 2.0) {
                        powerUpProximity += 15.0 / distance;
                    } else if (content == CellContent::BigMooseJuice && currentThreat > 1.0) {
                        powerUpProximity += 10.0 / distance;
                    }
                }
            }
        }
    }

    // 9. Exploration reward and penalty for repeated cell visits
    double explorationScore = 0.0;
    if (state.visitedCells.size() > 0) {
        int totalCells = state.getWidth() * state.getHeight();
        int visitedCells = static_cast<int>(state.visitedCells.size());
        double explorationRatio = static_cast<double>(visitedCells) / totalCells;
        
        // Reward good exploration ratios
        if (explorationRatio > 0.05) {
            explorationScore = explorationRatio * 200.0; // Increased reward for exploring
        }
        
        // Penalize low exploration ratios
        if (explorationRatio < 0.2) {
            explorationScore -= (0.2 - explorationRatio) * 200.0;
        }
    }

    // Final score calculation with strong weighting toward pellets but encouraging exploration
    double finalScore = pelletScore + distanceReward + streakBonus + immediateBonus 
                       - streakPenalty - threatPenalty + powerUpValue + powerUpProximity + explorationScore;

    return finalScore;
}

void MCTSEngine::runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId) {
    std::mt19937 localRng(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count() + threadId));
    
    while (!shouldStop.load()) {
        // Selection with virtual loss
        MCTSNode* selectedNode = select(root);
        
        // Expansion
        MCTSNode* nodeToSimulate = selectedNode;
        if (!selectedNode->isTerminalNode()) {
            if (selectedNode->tryLockExpansion()) {
                if (!selectedNode->isFullyExpandedNode()) { // Double check after lock
                    MCTSNode* expandedNode = expand(selectedNode);
                    if (expandedNode != selectedNode) { // Check if a *new* node was created
                        nodeToSimulate = expandedNode;
                        totalExpansions++;
                    }
                }
                selectedNode->unlockExpansion();
            }
        }
        
        // Simulation with action sequence tracking
        std::vector<BotAction> actionSequence;
        double reward = simulate(nodeToSimulate->getGameState(), playerId, actionSequence);
        totalSimulations++;
        
        // Backpropagation with AMAF update
        backpropagate(nodeToSimulate, reward, actionSequence);
        
        // Remove virtual loss from the path
        if (useVirtualLoss) {
            MCTSNode* current = nodeToSimulate;
            while (current && current != root) {
                virtualLoss->removeVirtualLoss(current);
                current = current->getParent();
            }
        }
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