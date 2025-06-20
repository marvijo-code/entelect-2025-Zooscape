#pragma once

#include "GameState.h"
#include "MCTSNode.h"
#include "Heuristics.h"

struct ActionStats {
    BotAction action;
    int visits;
    double avgScore;
};

struct MCTSResult {
    BotAction bestAction;
    std::vector<ActionStats> allActionStats;
};

#include <memory>
#include <vector>
#include <chrono>
#include <thread>
#include <atomic>
#include <mutex>
#include <random>

class MCTSEngine {
private:
    // MCTS parameters
    double explorationConstant;
    int maxIterations;
    int maxSimulationDepth;
    std::chrono::milliseconds timeLimit;
    
    // Threading
    int numThreads;
    std::atomic<bool> shouldStop;
    std::mutex treeMutex;
    
    // Random number generation
    thread_local static std::mt19937 rng;
    
    // Heuristics
    HeuristicsEngine heuristicsEngine;
    
    // Statistics
    mutable std::atomic<int> totalSimulations;
    mutable std::atomic<int> totalExpansions;
    
    // MCTS phases
    MCTSNode* select(MCTSNode* root);
    MCTSNode* expand(MCTSNode* node);
    double simulate(const GameState& state, const std::string& playerId);
    void backpropagate(MCTSNode* node, double reward);
    
    // Advanced MCTS techniques
    double calculateUCB1(const MCTSNode* node, const MCTSNode* parent) const;
    double calculateRAVE(const MCTSNode* node) const;
    bool shouldExpandNode(const MCTSNode* node) const;
    
    // Simulation policies
    BotAction selectSimulationAction(const GameState& state, const std::string& playerId);
    double evaluateTerminalState(const GameState& state, const std::string& playerId);
    
    // Threading support
    void runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId);
    
public:
    MCTSEngine(double explorationConstant, 
               int maxIterations,
               int maxSimulationDepth,
               int timeLimit, 
               int numThreads);
    
    ~MCTSEngine();
    
    // Main MCTS interface
    MCTSResult findBestAction(const GameState& state, const std::string& playerId);
    
    // Configuration
    void setExplorationConstant(double c) { explorationConstant = c; }
    void setMaxIterations(int iterations) { maxIterations = iterations; }
    void setTimeLimit(int milliseconds) { timeLimit = std::chrono::milliseconds(milliseconds); }
    void setNumThreads(int threads) { numThreads = threads; }
    
    // Statistics
    int getTotalSimulations() const { return totalSimulations.load(); }
    int getTotalExpansions() const { return totalExpansions.load(); }
    void resetStatistics() { totalSimulations = 0; totalExpansions = 0; }
    
    // Advanced features
    void enableProgressiveWidening(bool enable);
    void enableRAVE(bool enable);
    void setHeuristicWeight(double weight);
};

// UCB1-Tuned implementation
class UCB1Tuned {
private:
    static constexpr double CONFIDENCE_BOUND = 0.25;
    
public:
    static double calculate(const MCTSNode* node, const MCTSNode* parent, double explorationConstant);
};

// RAVE (Rapid Action Value Estimation) implementation
class RAVE {
private:
    std::unordered_map<BotAction, std::pair<double, int>> actionValues;
    double beta;
    
public:
    RAVE(double beta = 0.5) : beta(beta) {}
    
    void updateActionValue(BotAction action, double reward);
    double getActionValue(BotAction action) const;
    double calculateRAVEValue(const MCTSNode* node) const;
};

// Progressive Widening implementation
class ProgressiveWidening {
private:
    double alpha;
    double threshold;
    
public:
    ProgressiveWidening(double alpha = 0.5, double threshold = 1.0) 
        : alpha(alpha), threshold(threshold) {}
    
    bool shouldExpand(int visits, int children) const;
    int getMaxChildren(int visits) const;
};