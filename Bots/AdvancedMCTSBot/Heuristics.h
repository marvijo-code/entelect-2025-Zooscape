#pragma once

#include "GameState.h"
#include <unordered_map>
#include <vector>
#include <memory>

// Base heuristic interface
class IHeuristic {
public:
    virtual ~IHeuristic() = default;
    virtual double evaluate(const GameState& state, const std::string& playerId, BotAction action) const = 0;
    virtual std::string getName() const = 0;
    virtual double getWeight() const = 0;
    virtual void setWeight(double weight) = 0;
};

// Pellet collection heuristics
class PelletDistanceHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    PelletDistanceHeuristic(double w = 2.0) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "PelletDistance"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class PelletDensityHeuristic : public IHeuristic {
private:
    double weight;
    int searchRadius;
    
public:
    PelletDensityHeuristic(double w = 1.5, int radius = 5) : weight(w), searchRadius(radius) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "PelletDensity"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class ScoreStreakHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    ScoreStreakHeuristic(double w = 1.8) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "ScoreStreak"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

// Zookeeper avoidance heuristics
class ZookeeperAvoidanceHeuristic : public IHeuristic {
private:
    double weight;
    int dangerRadius;
    
public:
    ZookeeperAvoidanceHeuristic(double w = 5.0, int radius = 8) : weight(w), dangerRadius(radius) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "ZookeeperAvoidance"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class ZookeeperPredictionHeuristic : public IHeuristic {
private:
    double weight;
    int predictionSteps;
    
public:
    ZookeeperPredictionHeuristic(double w = 3.5, int steps = 5) : weight(w), predictionSteps(steps) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "ZookeeperPrediction"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

// Power-up heuristics
class PowerUpCollectionHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    PowerUpCollectionHeuristic(double w = 2.5) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "PowerUpCollection"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class PowerUpUsageHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    PowerUpUsageHeuristic(double w = 3.0) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "PowerUpUsage"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

// Movement and positioning heuristics
class CenterControlHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    CenterControlHeuristic(double w = 0.8) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "CenterControl"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class WallAvoidanceHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    WallAvoidanceHeuristic(double w = 1.2) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "WallAvoidance"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class MovementConsistencyHeuristic : public IHeuristic {
private:
    double weight;
    mutable std::unordered_map<std::string, BotAction> lastActions;
    
public:
    MovementConsistencyHeuristic(double w = 0.6) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "MovementConsistency"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

// Advanced strategic heuristics
class TerritoryControlHeuristic : public IHeuristic {
private:
    double weight;
    int controlRadius;
    
public:
    TerritoryControlHeuristic(double w = 1.4, int radius = 6) : weight(w), controlRadius(radius) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "TerritoryControl"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class OpponentBlockingHeuristic : public IHeuristic {
private:
    double weight;
    
public:
    OpponentBlockingHeuristic(double w = 1.0) : weight(w) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "OpponentBlocking"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

class EndgameHeuristic : public IHeuristic {
private:
    double weight;
    double endgameThreshold;
    
public:
    EndgameHeuristic(double w = 2.0, double threshold = 0.3) : weight(w), endgameThreshold(threshold) {}
    double evaluate(const GameState& state, const std::string& playerId, BotAction action) const override;
    std::string getName() const override { return "Endgame"; }
    double getWeight() const override { return weight; }
    void setWeight(double w) override { weight = w; }
};

// Heuristics engine that combines all heuristics
class HeuristicsEngine {
private:
    std::vector<std::unique_ptr<IHeuristic>> heuristics;
    std::unordered_map<std::string, double> heuristicWeights;
    bool enableLogging;
    
public:
    HeuristicsEngine(bool logging = false);
    ~HeuristicsEngine() = default;
    
    // Heuristic management
    void addHeuristic(std::unique_ptr<IHeuristic> heuristic);
    void removeHeuristic(const std::string& name);
    void setHeuristicWeight(const std::string& name, double weight);
    double getHeuristicWeight(const std::string& name) const;
    
    // Evaluation
    double evaluateAction(const GameState& state, const std::string& playerId, BotAction action) const;
    std::unordered_map<BotAction, double> evaluateAllActions(const GameState& state, const std::string& playerId) const;
    
    // Configuration
    void enableHeuristicLogging(bool enable) { enableLogging = enable; }
    void loadWeightsFromConfig(const std::string& configPath);
    void saveWeightsToConfig(const std::string& configPath) const;
    
    // Analysis
    std::vector<std::pair<std::string, double>> getHeuristicContributions(
        const GameState& state, const std::string& playerId, BotAction action) const;
    
    // Presets
    void loadAggressivePreset();
    void loadDefensivePreset();
    void loadBalancedPreset();
    void loadEndgamePreset();
};

// Utility functions for heuristic calculations
namespace HeuristicUtils {
    double calculateDistance(const Position& a, const Position& b);
    double calculateNormalizedDistance(const Position& a, const Position& b, int maxDistance);
    std::vector<Position> getPositionsInRadius(const Position& center, int radius, const GameState& state);
    double calculateAreaControl(const Position& center, int radius, const GameState& state, const std::string& playerId);
    bool isInDangerZone(const Position& pos, const std::vector<Zookeeper>& zookeepers, int dangerRadius);
    double calculatePelletValue(const GameState& state, const Position& pelletPos, const std::string& playerId);
}