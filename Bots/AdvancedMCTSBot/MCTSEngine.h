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
#include <unordered_map>

// Modern MCTS enhancement classes
class TranspositionTable {
private:
    struct StateEntry {
        std::string stateHash;
        std::weak_ptr<MCTSNode> node;
        int visits;
        double avgReward;
        std::chrono::steady_clock::time_point lastAccessed;
    };
    
    std::unordered_map<std::string, StateEntry> table;
    std::mutex tableMutex;
    size_t maxSize;
    
public:
    TranspositionTable(size_t maxSize = 100000) : maxSize(maxSize) {}
    
    std::shared_ptr<MCTSNode> lookup(const std::string& stateHash);
    void store(const std::string& stateHash, std::shared_ptr<MCTSNode> node);
    void cleanup(); // Remove expired weak_ptrs
    size_t size() const { return table.size(); }
};

class VirtualLoss {
private:
    std::unordered_map<MCTSNode*, std::atomic<double>> virtualLosses;
    mutable std::mutex lossMapMutex;
    double virtualLossValue;
    
public:
    VirtualLoss(double lossValue = 10.0) : virtualLossValue(lossValue) {}
    
    void addVirtualLoss(MCTSNode* node);
    void removeVirtualLoss(MCTSNode* node);
    double getVirtualLoss(MCTSNode* node) const;
};

class AMAF {
private:
    struct AMAFStats {
        std::atomic<double> totalReward{0.0};
        std::atomic<int> visits{0};
    };
    
    std::unordered_map<BotAction, AMAFStats> globalStats;
    mutable std::mutex statsMutex;
    double beta;
    
public:
    AMAF(double beta = 0.25) : beta(beta) {}
    
    void updateAMAF(const std::vector<BotAction>& sequence, double finalReward);
    double getAMAFValue(BotAction action) const;
    double combinedValue(double mctsValue, BotAction action, int mctsVisits) const;
};

// Enhanced bandit algorithms
class BanditAlgorithm {
public:
    virtual ~BanditAlgorithm() = default;
    virtual double calculateValue(const MCTSNode* node, const MCTSNode* parent) const = 0;
    virtual std::string getName() const = 0;
};

class EnhancedUCB1 : public BanditAlgorithm {
private:
    double explorationConstant;
    double progressiveBiasWeight;
    
public:
    EnhancedUCB1(double c = 1.414, double biasWeight = 0.5) 
        : explorationConstant(c), progressiveBiasWeight(biasWeight) {}
    
    double calculateValue(const MCTSNode* node, const MCTSNode* parent) const override;
    std::string getName() const override { return "Enhanced UCB1"; }
};

class UCB_V : public BanditAlgorithm {
private:
    double explorationConstant;
    double varianceScale;
    
public:
    UCB_V(double c = 1.0, double varScale = 0.25) 
        : explorationConstant(c), varianceScale(varScale) {}
    
    double calculateValue(const MCTSNode* node, const MCTSNode* parent) const override;
    std::string getName() const override { return "UCB-V"; }
};

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
    
    // Modern MCTS enhancements
    std::unique_ptr<TranspositionTable> transpositionTable;
    std::unique_ptr<VirtualLoss> virtualLoss;
    std::unique_ptr<AMAF> amaf;
    std::unique_ptr<BanditAlgorithm> banditAlgorithm;
    
    // Configuration flags
    bool useTranspositionTable;
    bool useVirtualLoss;
    bool useAMAF;
    bool useProgressiveWidening;
    bool useRAVE;
    double heuristicWeight;
    
    // Move ordering
    std::vector<BotAction> moveOrdering;
    
    // MCTS phases
    MCTSNode* select(MCTSNode* root);
    MCTSNode* expand(MCTSNode* node);
    double simulate(const GameState& state, const std::string& playerId, std::vector<BotAction>& actionSequence);
    void backpropagate(MCTSNode* node, double reward, const std::vector<BotAction>& actionSequence);
    
    // Advanced MCTS techniques
    double calculateUCB1(const MCTSNode* node, const MCTSNode* parent) const;
    double calculateRAVE(const MCTSNode* node) const;
    bool shouldExpandNode(const MCTSNode* node) const;
    
    // Simulation policies
    BotAction selectSimulationAction(const GameState& state, const std::string& playerId);
    double evaluateTerminalState(const GameState& state, const std::string& playerId);
    
    // Move ordering and pruning
    void initializeMoveOrdering(const GameState& state, const std::string& playerId);
    std::vector<BotAction> getOrderedMoves(const GameState& state, const std::string& playerId);
    bool shouldPruneMove(BotAction action, const GameState& state, const std::string& playerId);
    
    // State hashing for transposition table
    std::string hashGameState(const GameState& state, const std::string& playerId) const;
    
    // Helper method for position calculation
    Position getNewPosition(const Position& currentPos, BotAction action) const;
    
    // Threading support
    void runParallelMCTS(MCTSNode* root, const std::string& playerId, int threadId);
    
    // Time management
    bool shouldContinueSearch(std::chrono::steady_clock::time_point startTime) const;
    
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
    
    // Modern features configuration
    void enableTranspositionTable(bool enable) { useTranspositionTable = enable; }
    void enableVirtualLoss(bool enable) { useVirtualLoss = enable; }
    void enableAMAF(bool enable) { useAMAF = enable; }
    void setBanditAlgorithm(std::unique_ptr<BanditAlgorithm> algorithm) { banditAlgorithm = std::move(algorithm); }
    
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