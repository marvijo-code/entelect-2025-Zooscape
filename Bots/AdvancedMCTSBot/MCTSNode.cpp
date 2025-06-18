#include "MCTSNode.h"
#include "fmt/core.h" // Added for fmt::println
#include <algorithm>
#include <iostream>
#include <iomanip>
#include <sstream>
#include <random> // Added for std::mt19937 and std::uniform_int_distribution
#include <functional>

MCTSNode::MCTSNode(std::unique_ptr<GameState> state, MCTSNode* parent, 
                   BotAction action, const std::string& playerId)
    : gameState(std::move(state))
    , parent(parent)
    , action(action)
    , playerId(playerId)
    , visits(0)
    , totalReward(0.0)
    , totalSquaredReward(0.0)
    , isExpanding(false)
    , isTerminal(false)
    , isFullyExpanded(false)
    , cachedUCBValue(0.0)
    , cachedUCBVisits(-1) {
    
    isTerminal = gameState->isTerminal();
    if (isTerminal.load()) {
        isFullyExpanded = true;
    }
}

MCTSNode::~MCTSNode() = default;

MCTSNode* MCTSNode::select(double explorationConstant) {
    if (isTerminalNode() || !isFullyExpandedNode()) {
        return this;
    }
    
    double bestUCB = -std::numeric_limits<double>::infinity();
    MCTSNode* bestChild = nullptr;
    
    for (const auto& child : children) {
        double ucb = child->calculateUCB1Tuned(explorationConstant);
        if (ucb > bestUCB) {
            bestUCB = ucb;
            bestChild = child.get();
        }
    }
    
    return bestChild ? bestChild->select(explorationConstant) : this;
}

MCTSNode* MCTSNode::expand() {
    if (isTerminalNode() || isFullyExpandedNode()) {
        return this;
    }
    
    auto untriedActions = getUntriedActions();
    if (untriedActions.empty()) {
        markAsFullyExpanded();
        return this;
    }
    
    // Select a random action to expand. This is crucial for exploring the tree.
    // Using a thread_local RNG here is efficient as it's initialized once per thread.
        thread_local std::mt19937 generator(static_cast<unsigned int>(std::chrono::steady_clock::now().time_since_epoch().count()));
    std::uniform_int_distribution<size_t> distribution(0, untriedActions.size() - 1);
    BotAction actionToExpand = untriedActions[distribution(generator)];
    
    // Create a new state by cloning the current state and then applying the action
    auto newState = gameState->clone();
    newState->applyAction(this->playerId, actionToExpand);
    auto child = std::make_unique<MCTSNode>(std::move(newState), this, actionToExpand, playerId);
    MCTSNode* childPtr = child.get();
    
    children.push_back(std::move(child));
    
    // Check if fully expanded
    auto allLegalActions = gameState->getLegalActions(playerId);
    if (children.size() >= allLegalActions.size()) {
        markAsFullyExpanded();
    }
    
    return childPtr;
}

void MCTSNode::update(double reward) {
    atomicIncrementVisits();
    atomicAddReward(reward);
    
    // Update squared reward for variance calculation
    double currentSquaredReward = totalSquaredReward.load(std::memory_order_relaxed);
    double newSquaredReward;
    do {
        newSquaredReward = currentSquaredReward + (reward * reward);
    } while (!totalSquaredReward.compare_exchange_weak(currentSquaredReward, newSquaredReward, std::memory_order_release, std::memory_order_relaxed));
    
    // Invalidate cached UCB value
    cachedUCBVisits = -1;
}

double MCTSNode::calculateUCB1(double explorationConstant) const {
    if (visits.load() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    if (!parent) {
        return getAverageReward();
    }
    
    double exploitation = getAverageReward();
    double exploration = explorationConstant * 
                        std::sqrt(std::log(parent->getVisits()) / visits.load());
    
    return exploitation + exploration;
}

double MCTSNode::calculateUCB1Tuned(double explorationConstant) const {
    if (visits.load() == 0) {
        return std::numeric_limits<double>::infinity();
    }
    
    if (!parent) {
        return getAverageReward();
    }
    
    // Check if cached value is still valid
    int currentVisits = visits.load();
    if (cachedUCBVisits.load() == currentVisits) {
        return cachedUCBValue.load();
    }
    
    double exploitation = getAverageReward();
    double logParentVisits = std::log(parent->getVisits());
    double nodeVisits = static_cast<double>(currentVisits);
    
    // Calculate variance term
    double variance = getRewardVariance();
    double varianceBound = variance + std::sqrt(2 * logParentVisits / nodeVisits);
    double exploration = explorationConstant * 
                        std::sqrt(logParentVisits / nodeVisits * std::min(0.25, varianceBound));
    
    double ucbValue = exploitation + exploration;
    
    // Cache the result
    cachedUCBValue = ucbValue;
    cachedUCBVisits = currentVisits;
    
    return ucbValue;
}

double MCTSNode::calculateRAVEValue() const {
    // RAVE implementation would combine MCTS and RAVE values
    // For now, just return MCTS value
    return getAverageReward();
}

bool MCTSNode::hasUntriedActions() const {
    if (isTerminalNode()) return false;
    
    auto legalActions = gameState->getLegalActions(playerId);
    return children.size() < legalActions.size();
}

double MCTSNode::getAverageReward() const {
    int v = visits.load();
    return v > 0 ? totalReward.load() / v : 0.0;
}

double MCTSNode::getRewardVariance() const {
    int v = visits.load();
    if (v <= 1) return 0.0;
    
    double mean = getAverageReward();
    double meanSquared = totalSquaredReward.load() / v;
    return meanSquared - mean * mean;
}

MCTSNode* MCTSNode::getBestChild(double explorationConstant) const {
    if (children.empty()) return nullptr;
    
    if (explorationConstant == 0.0) {
        // Pure exploitation - select child with highest average reward
        auto it = std::max_element(children.begin(), children.end(),
            [](const std::unique_ptr<MCTSNode>& a, const std::unique_ptr<MCTSNode>& b) {
                return a->getAverageReward() < b->getAverageReward();
            });
        return it->get();
    } else {
        // UCB-based selection
        auto it = std::max_element(children.begin(), children.end(),
            [explorationConstant](const std::unique_ptr<MCTSNode>& a, const std::unique_ptr<MCTSNode>& b) {
                return a->calculateUCB1Tuned(explorationConstant) < b->calculateUCB1Tuned(explorationConstant);
            });
        return it->get();
    }
}

MCTSNode* MCTSNode::getMostVisitedChild() const {
    if (children.empty()) return nullptr;
    
    auto it = std::max_element(children.begin(), children.end(),
        [](const std::unique_ptr<MCTSNode>& a, const std::unique_ptr<MCTSNode>& b) {
            return a->getVisits() < b->getVisits();
        });
    return it->get();
}

void MCTSNode::updateRAVE(BotAction action, double reward) {
    auto& [totalReward, visits] = raveStats[action];
    double currentRaveReward = totalReward.load(std::memory_order_relaxed);
    double newRaveReward;
    do {
        newRaveReward = currentRaveReward + reward;
    } while (!totalReward.compare_exchange_weak(currentRaveReward, newRaveReward, std::memory_order_release, std::memory_order_relaxed));
    visits.fetch_add(1, std::memory_order_relaxed); // fetch_add for int is fine
}

double MCTSNode::getRAVEValue(BotAction action) const {
    auto it = raveStats.find(action);
    if (it != raveStats.end() && it->second.second.load() > 0) {
        return it->second.first.load() / it->second.second.load();
    }
    return 0.0;
}

int MCTSNode::getRAVEVisits(BotAction action) const {
    auto it = raveStats.find(action);
    return it != raveStats.end() ? it->second.second.load() : 0;
}

int MCTSNode::getDepth() const {
    int depth = 0;
    const MCTSNode* current = this;
    while (current->parent != nullptr) {
        depth++;
        current = current->parent;
    }
    return depth;
}

int MCTSNode::getTreeSize() const {
    int size = 1;
    for (const auto& child : children) {
        size += child->getTreeSize();
    }
    return size;
}

std::vector<BotAction> MCTSNode::getPathFromRoot() const {
    std::vector<BotAction> path;
    const MCTSNode* current = this;
    
    while (current->parent != nullptr) {
        path.push_back(current->action);
        current = current->parent;
    }
    
    std::reverse(path.begin(), path.end());
    return path;
}

void MCTSNode::printTree(int maxDepth, int currentDepth) const {
    if (currentDepth > maxDepth) return;
    
    std::string indent(currentDepth * 2, ' ');
    std::cout << indent << "Action: " << static_cast<int>(action) 
              << ", Visits: " << visits.load()
              << ", Avg Reward: " << std::fixed << std::setprecision(3) << getAverageReward()
              << ", UCB: " << calculateUCB1Tuned(1.414) << std::endl;
    
    for (const auto& child : children) {
        child->printTree(maxDepth, currentDepth + 1);
    }
}

void MCTSNode::printStatistics() const {
    std::cout << "=== Node Statistics ===" << std::endl;
    std::cout << "Action: " << static_cast<int>(action) << std::endl;
    std::cout << "Visits: " << visits.load() << std::endl;
    std::cout << "Total Reward: " << totalReward.load() << std::endl;
    std::cout << "Average Reward: " << getAverageReward() << std::endl;
    std::cout << "Reward Variance: " << getRewardVariance() << std::endl;
    std::cout << "Children: " << children.size() << std::endl;
    std::cout << "Depth: " << getDepth() << std::endl;
    std::cout << "Tree Size: " << getTreeSize() << std::endl;
    std::cout << "Is Terminal: " << isTerminalNode() << std::endl;
    std::cout << "Is Fully Expanded: " << isFullyExpandedNode() << std::endl;
}

std::string MCTSNode::toString() const {
    std::ostringstream oss;
    oss << "MCTSNode[Action=" << static_cast<int>(action)
        << ", Visits=" << visits.load()
        << ", AvgReward=" << std::fixed << std::setprecision(3) << getAverageReward()
        << ", Children=" << children.size() << "]";
    return oss.str();
}

std::vector<BotAction> MCTSNode::getUntriedActions() const {
    auto legalActions = gameState->getLegalActions(playerId);
    std::vector<BotAction> untriedActions;
    
    for (const auto& action : legalActions) {
        bool found = false;
        for (const auto& child : children) {
            if (child->action == action) {
                found = true;
                break;
            }
        }
        if (!found) {
            untriedActions.push_back(action);
        }
    }
    
    return untriedActions;
}

void MCTSNode::markAsTerminal() {
    isTerminal = true;
    isFullyExpanded = true;
}

void MCTSNode::markAsFullyExpanded() {
    isFullyExpanded = true;
}

void MCTSNode::updateCachedValues() const {
    // Update cached UCB value if needed
    cachedUCBVisits = -1; // Invalidate cache
}

void MCTSNode::atomicAddReward(double reward) {
    // Atomic addition for thread safety
    double expected = totalReward.load();
    while (!totalReward.compare_exchange_weak(expected, expected + reward)) {
        // Retry until successful
    }
}

void MCTSNode::atomicIncrementVisits() {
    visits.fetch_add(1);
}

// TreeStatistics implementation
TreeStatistics TreeStatistics::analyze(const MCTSNode* root) {
    TreeStatistics stats;
    
    if (!root) return stats;
    
    std::function<void(const MCTSNode*, int)> traverse = [&](const MCTSNode* node, int depth) {
        stats.totalNodes++;
        stats.maxDepth = std::max(stats.maxDepth, depth);
        stats.totalVisits += node->getVisits();
        stats.averageReward += node->getAverageReward();
        
        if (!node->getChildren().empty()) {
            stats.averageBranchingFactor += node->getChildren().size();
        }
        
        for (const auto& child : node->getChildren()) {
            traverse(child.get(), depth + 1);
        }
    };
    
    traverse(root, 0);
    
    if (stats.totalNodes > 0) {
        stats.averageReward /= stats.totalNodes;
        stats.averageBranchingFactor /= stats.totalNodes;
    }
    
    return stats;
}