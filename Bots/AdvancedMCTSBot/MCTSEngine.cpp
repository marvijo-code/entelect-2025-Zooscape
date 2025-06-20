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
    
    while (!simState.isTerminal() && depth < maxSimulationDepth) {
        auto legalActions = simState.getLegalActions(playerId);
        if (legalActions.empty()) {
            break;
        }
        
        BotAction action = selectSimulationAction(simState, playerId);
        simState.applyAction(playerId, action);

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
        // Terminate rollout early if captured
        if (simState.isPlayerCaught(playerId)) {
            break;
        }

        depth++;
    }
    
    return evaluateTerminalState(simState, playerId);
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

    // --- Progressive Bias ---
    // Heuristic: inverse distance to nearest pellet from this child state.
    const GameState& childState = node->getGameState();
    const Animal* childAnimal = childState.getAnimal(childState.myAnimalId);
    double heuristicBias = 0.0;
    if (childAnimal) {
        int dist = childState.distanceToNearestPellet(childAnimal->position);
        if (dist >= 0) {
            heuristicBias = 1.0 / (dist + 1); // Closer pellets -> higher bias
        }
    }
    // Weight and decay with visits (progressive): bias / (1 + visits)
    const double biasWeight = 5.0; // Tuneable
    double progressiveBias = biasWeight * heuristicBias / (1.0 + node->getVisits());

    // Depth-dependent exploration constant (decays with depth)
    const int depth = node->getDepth();
    const double depthDecayFactor = 0.5;               // Tuneable: larger → faster decay
    double effectiveC = explorationConstant / (1.0 + depth * depthDecayFactor);

    // Standard UCB1 components with depth decay
    double exploitation = node->getAverageReward();
    double exploration = effectiveC * std::sqrt(std::log(static_cast<double>(parent->getVisits())) / node->getVisits());

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
    
    // Use heuristics to bias action selection
    auto actionScores = heuristicsEngine.evaluateAllActions(state, playerId);
    
    // Convert scores to probabilities using softmax
    std::vector<double> probabilities;
    double maxScore = -std::numeric_limits<double>::infinity();
    for (const auto& action : legalActions) {
        maxScore = std::max(maxScore, actionScores[action]);
    }
    
    double sumExp = 0.0;
    for (const auto& action : legalActions) {
        double exp_val = std::exp((actionScores[action] - maxScore) * 2.0); // Temperature = 0.5
        probabilities.push_back(exp_val);
        sumExp += exp_val;
    }
    
    // Normalize probabilities
    for (auto& prob : probabilities) {
        prob /= sumExp;
    }
    
    // Sample action based on probabilities
    std::uniform_real_distribution<double> dist(0.0, 1.0);
    double random = dist(rng);
    
    double cumulative = 0.0;
    for (size_t i = 0; i < legalActions.size(); ++i) {
        cumulative += probabilities[i];
        if (random <= cumulative) {
            return legalActions[i];
        }
    }
    
    return legalActions.back(); // Fallback
}

double MCTSEngine::evaluateTerminalState(const GameState& state, const std::string& playerId) {
    const Animal* animal = state.getAnimal(playerId);
    if (!animal) {
        return 0.0; // Should not happen
    }

    // 1. Pellet Score (Primary Reward)
    double pelletScore = static_cast<double>(animal->score);

    // 2. Distance to nearest pellet (encourage moving towards pellets)
    int distToPellet = state.distanceToNearestPellet(animal->position);
    double distanceReward = 0.0;
    if (distToPellet >= 0) {
        // Closer is better. Scale so that 0 distance yields max reward (e.g., 20) and far yields smaller.
        // Assume max meaningful Manhattan distance is (width + height).
        int maxDist = state.getWidth() + state.getHeight();
        maxDist = std::max(1, maxDist); // Prevent division by zero.
        distanceReward = static_cast<double>(maxDist - distToPellet);
    }

    // 3. Exploration bonus (encourage covering new tiles)
    double explorationBonus = static_cast<double>(state.visitedCells.size());

    // 4. Threat penalty (avoid zookeepers)
    double threatPenalty = state.getZookeeperThreat(animal->position);

    // 5. Capture penalty
    double capturePenalty = state.isPlayerCaught(playerId) ? 1000.0 : 0.0;

    // --- Weights --- (tuned for balanced ranges)
    const double pelletWeight      = 1.0;    // Treat accumulated score at face value
    const double distanceWeight    = 30.0;  // Incentive to approach pellets
    const double explorationWeight = 0.5;
    const double threatWeight      = 5.0;
    const double emptyPenaltyWeight = 200.0; // Penalty for long idle stretches without pellets
    const double instantPelletRewardWeight = 0.0;  // Immediate pellet worth exactly its base value via pelletScore – no extra bonus
    const double powerUpWeight     = 50.0;   // Reward active power-up usage

    // --- Compute raw score ---
    // 6. Empty-cell penalty (ticks since last pellet)
    double emptyPenalty = animal->ticksSinceLastPellet * emptyPenaltyWeight;
    // Reward if pellet collected this tick (ticksSinceLastPellet == 0)
    double instantPelletReward = 0.0; // No special bonus – already accounted for in pelletScore

    // 7. Power-up reward (active duration remaining)
    double powerUpReward = 0.0;
    if (animal->heldPowerUp != PowerUpType::None || animal->powerUpDuration > 0) {
        powerUpReward = powerUpWeight * (animal->powerUpDuration + 1); // favour collecting/using
    }

    double rawScore = (pelletWeight * pelletScore) +
                      (distanceWeight * distanceReward) +
                      (explorationWeight * explorationBonus) -
                      (threatWeight * threatPenalty) -
                      emptyPenalty +
                      powerUpReward +
                      instantPelletReward -
                      capturePenalty;

    // --- Scale the raw score into a bounded range using tanh to keep UCB exploitation comparable with exploration term ---
    // Chosen scale factor based on empirical max game scores; tune if necessary.
    const double scaleFactor = 20000.0;
    double scaled = std::tanh(rawScore / scaleFactor); // in (-1,1)
    // Map to [0,100] positive range for convenience
    return (scaled + 1.0) * 50.0;
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