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
#include <random>
#include <chrono>
#include <thread>
#include <atomic>
#include <mutex>

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
    std::unique_ptr<HeuristicsEngine> heuristics;
    
    // Statistics
    mutable std::atomic<int> totalSimulations;
    mutable std::atomic<int> totalExpansions;
    
    // MCTS phases
    MCTSNode* select(MCTSNode* root);
    MCTSNode* expand(MCTSNode* node);
    double simulate(const GameState& state, const std::string& playerId);
    void backpropagate(MCTSNode* node, double reward);
    
    // Advanced MCTS techniques
    double calculateUCB1Tuned(const MCTSNode* node, const MCTSNode* parent) const;
    double calculateRAVE(const MCTSNode* node) const;
    bool shouldExpandNode(const MCTSNode* node) const;
    
    // Simulation policies
    BotAction selectSimulationAction(const GameState& state, const std::string& playerId);
    double evaluateTerminalState(const GameState& state, const std::string& playerId);
    
    // Threading support
    void runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId);
    
public:
    MCTSEngine(double explorationConstant = 1.414, 
               int maxIterations = 10000,
               int maxSimulationDepth = 200,
               int timeLimit = 950, // milliseconds
               int numThreads = std::thread::hardware_concurrency());
    
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