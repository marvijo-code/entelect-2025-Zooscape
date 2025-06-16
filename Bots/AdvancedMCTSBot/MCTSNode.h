#pragma once

#include "GameState.h"
#include <vector>
#include <memory>
#include <unordered_map>
#include <atomic>
#include <mutex>

class MCTSNode {
private:
    // Node state
    std::unique_ptr<GameState> gameState;
    MCTSNode* parent;
    std::vector<std::unique_ptr<MCTSNode>> children;
    
    // MCTS statistics
    std::atomic<int> visits;
    std::atomic<double> totalReward;
    std::atomic<double> totalSquaredReward; // For UCB1-Tuned
    
    // Action that led to this node
    BotAction action;
    std::string playerId;
    
    // RAVE statistics
    std::unordered_map<BotAction, std::pair<std::atomic<double>, std::atomic<int>>> raveStats;
    
    // Threading support
    mutable std::mutex expansionMutex;
    std::atomic<bool> isExpanding;
    
    // Node properties
    std::atomic<bool> isTerminal;
    std::atomic<bool> isFullyExpanded;
    
    // Cached values
    mutable std::atomic<double> cachedUCBValue;
    mutable std::atomic<int> cachedUCBVisits;
    
public:
    MCTSNode(std::unique_ptr<GameState> state, MCTSNode* parent = nullptr, 
             BotAction action = BotAction::Up, const std::string& playerId = "");
    
    ~MCTSNode();
    
    // Core MCTS operations
    MCTSNode* select(double explorationConstant);
    MCTSNode* expand();
    void update(double reward);
    
    // UCB calculations
    double calculateUCB1(double explorationConstant) const;
    double calculateUCB1Tuned(double explorationConstant) const;
    double calculateRAVEValue() const;
    
    // Node properties
    bool isLeaf() const { return children.empty(); }
    bool hasUntriedActions() const;
    bool isTerminalNode() const { return isTerminal.load(); }
    bool isFullyExpandedNode() const { return isFullyExpanded.load(); }
    
    // Statistics
    int getVisits() const { return visits.load(); }
    double getAverageReward() const;
    double getRewardVariance() const;
    double getTotalReward() const { return totalReward.load(); }
    
    // Tree navigation
    MCTSNode* getParent() const { return parent; }
    const std::vector<std::unique_ptr<MCTSNode>>& getChildren() const { return children; }
    MCTSNode* getBestChild(double explorationConstant = 0.0) const;
    MCTSNode* getMostVisitedChild() const;
    
    // Game state access
    const GameState& getGameState() const { return *gameState; }
    BotAction getAction() const { return action; }
    const std::string& getPlayerId() const { return playerId; }
    
    // RAVE support
    void updateRAVE(BotAction action, double reward);
    double getRAVEValue(BotAction action) const;
    int getRAVEVisits(BotAction action) const;
    
    // Tree utilities
    int getDepth() const;
    int getTreeSize() const;
    std::vector<BotAction> getPathFromRoot() const;
    
    // Threading support
    void lockExpansion() { expansionMutex.lock(); }
    void unlockExpansion() { expansionMutex.unlock(); }
    bool tryLockExpansion() { return expansionMutex.try_lock(); }
    
    // Debugging and analysis
    void printTree(int maxDepth = 3, int currentDepth = 0) const;
    void printStatistics() const;
    std::string toString() const;
    
private:
    // Helper methods
    std::vector<BotAction> getUntriedActions() const;
    void markAsTerminal();
    void markAsFullyExpanded();
    void updateCachedValues() const;
    
    // Thread-safe operations
    void atomicAddReward(double reward);
    void atomicIncrementVisits();
};

// Node comparison functors
struct NodeComparator {
    bool operator()(const std::unique_ptr<MCTSNode>& a, const std::unique_ptr<MCTSNode>& b) const {
        return a->getAverageReward() > b->getAverageReward();
    }
};

struct NodeVisitComparator {
    bool operator()(const std::unique_ptr<MCTSNode>& a, const std::unique_ptr<MCTSNode>& b) const {
        return a->getVisits() > b->getVisits();
    }
};

// Tree statistics collector
class TreeStatistics {
public:
    int totalNodes;
    int maxDepth;
    int totalVisits;
    double averageBranchingFactor;
    double averageReward;
    
    TreeStatistics() : totalNodes(0), maxDepth(0), totalVisits(0), 
                      averageBranchingFactor(0.0), averageReward(0.0) {}
    
    static TreeStatistics analyze(const MCTSNode* root);
};