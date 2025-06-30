# ClingyHeuroBot2 - Genetic Algorithm Evolution Bot

A sophisticated bot for the Zooscape game that uses genetic algorithms to evolve optimal heuristic weights through gameplay.

## 🧬 **How the Genetic Algorithm Works**

### **Evolution System Architecture**
- **Population**: 20 individuals per generation
- **Generations**: Currently at Generation 61+ with 30MB of evolution data
- **Fitness Function**: Multi-objective optimization of score, survival time, rank, and efficiency
- **Selection Methods**: Tournament, Roulette Wheel, Rank-based
- **Crossover Operations**: Uniform, Blend (BLX-α), Arithmetic
- **Mutation Strategies**: Gaussian, Uniform, Adaptive

### **Real-time Evolution Process**
1. **Individual Assignment**: Bots get assigned evolved individuals with unique heuristic weights
2. **Performance Recording**: Every game records score, rank, survival time, captures
3. **Fitness Evaluation**: Multi-objective fitness calculation from game performance
4. **Automatic Evolution**: Triggers every 5 games or when sufficient performance data available
5. **Weight Updates**: Best evolved weights dynamically loaded every 30 seconds

## 📊 **Best Performers System**

### **Problem Solved**
- **Challenge**: How to preserve evolutionary breakthroughs while keeping git repository clean?
- **Solution**: Export/import system for elite individuals

### **Export System**
```bash
# Export current best 5 individuals
powershell -ExecutionPolicy Bypass -File export-current-best.ps1

# OR use the built-in C# export
dotnet run -- --export-best
```

**Files Created:**
- `best-individuals.json` - **Committed to git** for team sharing
- `best-individuals-genXXXX-timestamp.json` - Timestamped backup

### **Import System**
The bot automatically imports best individuals when:
- Starting with no existing evolution data
- Evolution data is corrupted/missing
- Fresh git clone on new machine

## 🗂️ **File Structure & Git Strategy**

### **Committed to Git (✅)**
```
Bots/ClingyHeuroBot2/
├── GeneticAlgorithm/           # Core GA implementation
├── Heuristics/                 # 54+ heuristic implementations
├── Services/                   # Bot service logic
├── *.cs                       # All source code
├── *.csproj                   # Project files
├── appsettings.json           # Configuration
├── heuristic-weights.json     # Static fallback weights
├── best-individuals.json      # ✅ EXPORTED ELITE PERFORMERS
├── export-current-best.ps1    # Export utility script
├── evolution-data/.gitkeep    # Directory placeholder
└── .gitignore                 # Git rules
```

### **Ignored by Git (❌)**
```
evolution-data/                # 30MB+ of generated data
├── current-population/        # ❌ Active 20 individuals  
├── backups/generation-*/      # ❌ 61+ generations of history
├── high-scores.json          # ❌ Performance tracking
└── *.json                    # ❌ 3,546+ individual files
```

## 🚀 **Usage Instructions**

### **Development Workflow**
1. **Code Changes**: Modify heuristics, GA parameters, bot logic
2. **Test Locally**: Run `ra-run-all-local.ps1` to test with evolved weights
3. **Export Winners**: Run export script after significant improvements
4. **Commit & Share**: Git commit includes source + best performers
5. **Team Benefits**: Other developers get proven good individuals

### **Fresh Environment Setup**
1. **Clone Repository**: `git clone <repo>`
2. **First Run**: Bot auto-imports best individuals from `best-individuals.json`
3. **Evolution Continues**: New evolution starts from proven elite base
4. **No Loss**: Evolutionary progress preserved while allowing fresh innovation

### **Running the Bot**
```bash
# Normal gameplay (connects to server)
dotnet run

# Export current best individuals
powershell -ExecutionPolicy Bypass -File export-current-best.ps1

# View evolution statistics
# (Built into bot console output)
```

## 📈 **Current Status (Generation 61+)**

- **✅ 61+ Generations Evolved** 
- **✅ 30MB Evolution Data** (locally stored)
- **✅ 3,546+ Individual Files** (locally generated) 
- **✅ 54 Heuristics** (AdaptivePathfinding, AnimalCongestion, AreaControl, etc.)
- **✅ Multi-objective Fitness** (Score + Survival + Rank + Efficiency)
- **✅ Real-time Weight Updates** (Every 30 seconds)
- **✅ Automatic Performance Recording** (Every game completion)
- **✅ Smart Individual Assignment** (Prioritizes stale individuals)

## 🎯 **Benefits of This System**

### **For Development**
- **🔬 Reproducible**: Source code + best individuals = identical starting point
- **🤝 Collaborative**: Team shares evolutionary breakthroughs via git
- **🚀 Fast Bootstrap**: New environments start with proven performers
- **📊 Clean Repository**: No 30MB of evolution data bloating git

### **For Evolution**
- **🧬 Continuous Innovation**: Each environment evolves independently
- **🏆 Elite Preservation**: Best performers never lost
- **⚡ Fresh Starts**: Can restart evolution while keeping elite base
- **🎲 Diverse Exploration**: Different machines explore different paths

## 🔧 **Configuration**

Key parameters in `GeneticAlgorithmConfig`:
- **PopulationSize**: 20 individuals
- **Elite Count**: 5 (preserved each generation)
- **Crossover Rate**: 0.8 (80% chance)
- **Mutation Rate**: 0.1 (10% chance)
- **Selection Method**: Tournament selection
- **Stagnation Limit**: 20 generations without improvement

## 📋 **Monitoring Evolution**

The bot provides real-time evolution statistics:
```
=== EVOLUTION STATISTICS ===
Generation: 61
Population Size: 20
Average Fitness: 2.45
Best Fitness: 8.32
Population Diversity: 0.67
Total Games Played: 1,247
Evolution Running: true
Games Since Last Evolution: 3
Best Individual: Gen 56, 15 games, Fitness 8.32
Running Bots: ClingyHeuroBot2
============================
```

---

**This system gives you the best of both worlds: cutting-edge genetic algorithm evolution with practical version control for team development!** 🧬⚡ 