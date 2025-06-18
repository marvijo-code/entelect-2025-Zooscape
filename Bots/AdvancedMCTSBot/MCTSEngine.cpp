#include "MCTSEngine.h"
#include <algorithm>
#include <cmath>
#include <chrono>
#include <future>
#include <iostream>

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
    , totalExpansions(0) {
    
    heuristics = std::make_unique<HeuristicsEngine>(false);
    heuristics->loadBalancedPreset();
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
            if (!selectedNode->isTerminalNode() && selectedNode->getVisits() > 0) {
                nodeToSimulate = expand(selectedNode);
                if (nodeToSimulate) {
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
        double bestUCB = -std::numeric_limits<double>::infinity();
        MCTSNode* bestChild = nullptr;
        
        for (const auto& child : current->getChildren()) {
            double ucb = calculateUCB1Tuned(child.get(), current);
            if (ucb > bestUCB) {
                bestUCB = ucb;
                bestChild = child.get();
            }
        }
        
        if (bestChild) {
            current = bestChild;
        } else {
            break;
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
        simState = simState.applyAction(playerId, action);
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

double MCTSEngine::calculateUCB1Tuned(const MCTSNode* node, const MCTSNode* parent) const {
    if (node->getVisits() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    double exploitation = node->getAverageReward();
    double exploration = explorationConstant * std::sqrt(std::log(parent->getVisits()) / node->getVisits());
    
    // UCB1-Tuned variance term
    double variance = node->getRewardVariance();
    double varianceTerm = std::min(0.25, variance + std::sqrt(2 * std::log(parent->getVisits()) / node->getVisits()));
    exploration *= std::sqrt(varianceTerm);
    
    return exploitation + exploration;
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
    auto actionScores = heuristics->evaluateAllActions(state, playerId);
    
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
    double baseScore = state.evaluate(playerId);
    
    // Normalize score to [0, 100] range for MCTS
    // Assuming scores can range from -2000 to 10000 for a wider, more dynamic range.
    const double minScore = -2000.0;
    const double maxScore = 10000.0;
    double normalizedScore = (baseScore - minScore) / (maxScore - minScore);
    return std::max(0.0, std::min(100.0, normalizedScore * 100.0));
}

void MCTSEngine::runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId) {
        std::mt19937 localRng(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count() + threadId));
    
    while (!shouldStop.load()) {
        // Selection
        MCTSNode* selectedNode = select(root);
        
        // Expansion
        MCTSNode* nodeToSimulate = selectedNode;
        if (!selectedNode->isTerminalNode() && selectedNode->getVisits() > 0) {
            if (selectedNode->tryLockExpansion()) {
                if (!selectedNode->isFullyExpandedNode()) {
                    nodeToSimulate = selectedNode->expand();
                    if (nodeToSimulate) {
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