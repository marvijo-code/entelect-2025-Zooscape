#pragma warning disable SKEXP0110

using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.GeneticAlgorithm;

/// <summary>
/// Implements genetic algorithm operators: selection, crossover, and mutation
/// </summary>
public static class GeneticOperators
{
    // Static fields for Box-Muller transform
    private static double _u1 = 0, _u2 = 0;
    private static bool _hasSpare = false;

    /// <summary>
    /// Tournament selection - selects the best individual from a random tournament
    /// </summary>
    public static Individual TournamentSelection(Population population, int tournamentSize, Random random)
    {
        if (population.Individuals.Count == 0)
            throw new InvalidOperationException("Cannot select from empty population");

        tournamentSize = Math.Min(tournamentSize, population.Individuals.Count);
        
        var tournament = new List<Individual>();
        for (int i = 0; i < tournamentSize; i++)
        {
            var randomIndex = random.Next(population.Individuals.Count);
            tournament.Add(population.Individuals[randomIndex]);
        }

        return tournament.OrderByDescending(i => i.Fitness).First();
    }

    /// <summary>
    /// Roulette wheel selection - selects individuals based on fitness proportions
    /// </summary>
    public static Individual RouletteWheelSelection(Population population, Random random)
    {
        if (population.Individuals.Count == 0)
            throw new InvalidOperationException("Cannot select from empty population");

        // Calculate total fitness (shift if negative values exist)
        var minFitness = population.Individuals.Min(i => i.Fitness);
        var offset = minFitness < 0 ? Math.Abs(minFitness) + 1 : 0;
        var totalFitness = population.Individuals.Sum(i => i.Fitness + offset);

        if (totalFitness <= 0)
        {
            // Fallback to random selection if all fitness values are zero or negative
            var randomIndex = random.Next(population.Individuals.Count);
            return population.Individuals[randomIndex];
        }

        var randomValue = random.NextDouble() * totalFitness;
        var cumulativeFitness = 0.0;

        foreach (var individual in population.Individuals)
        {
            cumulativeFitness += individual.Fitness + offset;
            if (cumulativeFitness >= randomValue)
                return individual;
        }

        // Fallback (should not reach here)
        return population.Individuals.Last();
    }

    /// <summary>
    /// Rank-based selection - selects individuals based on their rank rather than raw fitness
    /// </summary>
    public static Individual RankSelection(Population population, Random random)
    {
        if (population.Individuals.Count == 0)
            throw new InvalidOperationException("Cannot select from empty population");

        var rankedIndividuals = population.Individuals
            .OrderBy(i => i.Fitness)
            .Select((individual, index) => new { Individual = individual, Rank = index + 1 })
            .ToList();

        var totalRankSum = rankedIndividuals.Sum(r => r.Rank);
        var randomValue = random.NextDouble() * totalRankSum;
        var cumulativeRank = 0.0;

        foreach (var ranked in rankedIndividuals)
        {
            cumulativeRank += ranked.Rank;
            if (cumulativeRank >= randomValue)
                return ranked.Individual;
        }

        return rankedIndividuals.Last().Individual;
    }

    /// <summary>
    /// Uniform crossover - each gene has a 50% chance of coming from either parent
    /// </summary>
    public static (Individual child1, Individual child2) UniformCrossover(
        Individual parent1, Individual parent2, Random random)
    {
        var child1Genome = parent1.Clone().Genome;
        var child2Genome = parent2.Clone().Genome;

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (random.NextDouble() < 0.5)
            {
                // Swap the values between children
                var temp = property.GetValue(child1Genome);
                property.SetValue(child1Genome, property.GetValue(child2Genome));
                property.SetValue(child2Genome, temp);
            }
        }

        var child1 = new Individual(parent1, parent2, "UniformCrossover") { Genome = child1Genome };
        var child2 = new Individual(parent1, parent2, "UniformCrossover") { Genome = child2Genome };

        return (child1, child2);
    }

    /// <summary>
    /// Blend crossover (BLX-α) - creates children by blending parent values with some randomness
    /// </summary>
    public static (Individual child1, Individual child2) BlendCrossover(
        Individual parent1, Individual parent2, double alpha, Random random)
    {
        var child1Genome = new HeuristicWeights();
        var child2Genome = new HeuristicWeights();

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(decimal))
            {
                var value1 = (decimal)property.GetValue(parent1.Genome);
                var value2 = (decimal)property.GetValue(parent2.Genome);
                
                var minValue = Math.Min(value1, value2);
                var maxValue = Math.Max(value1, value2);
                var range = maxValue - minValue;
                var extendedMin = minValue - (decimal)(alpha * (double)range);
                var extendedMax = maxValue + (decimal)(alpha * (double)range);

                var child1Value = (decimal)(random.NextDouble() * (double)(extendedMax - extendedMin)) + extendedMin;
                var child2Value = (decimal)(random.NextDouble() * (double)(extendedMax - extendedMin)) + extendedMin;

                property.SetValue(child1Genome, child1Value);
                property.SetValue(child2Genome, child2Value);
            }
            else if (property.PropertyType == typeof(int))
            {
                var value1 = (int)property.GetValue(parent1.Genome);
                var value2 = (int)property.GetValue(parent2.Genome);
                
                var minValue = Math.Min(value1, value2);
                var maxValue = Math.Max(value1, value2);
                var range = maxValue - minValue;
                var extendedMin = (int)(minValue - alpha * range);
                var extendedMax = (int)(maxValue + alpha * range);

                // Ensure positive values for int properties
                extendedMin = Math.Max(0, extendedMin);
                extendedMax = Math.Max(1, extendedMax);

                var child1Value = random.Next(extendedMin, extendedMax + 1);
                var child2Value = random.Next(extendedMin, extendedMax + 1);

                property.SetValue(child1Genome, child1Value);
                property.SetValue(child2Genome, child2Value);
            }
        }

        var child1 = new Individual(parent1, parent2, "BlendCrossover") { Genome = child1Genome };
        var child2 = new Individual(parent1, parent2, "BlendCrossover") { Genome = child2Genome };

        return (child1, child2);
    }

    /// <summary>
    /// Arithmetic crossover - creates children as weighted averages of parents
    /// </summary>
    public static (Individual child1, Individual child2) ArithmeticCrossover(
        Individual parent1, Individual parent2, double weight, Random random)
    {
        var child1Genome = new HeuristicWeights();
        var child2Genome = new HeuristicWeights();

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(decimal))
            {
                var value1 = (decimal)property.GetValue(parent1.Genome);
                var value2 = (decimal)property.GetValue(parent2.Genome);

                var child1Value = (decimal)(weight * (double)value1 + (1 - weight) * (double)value2);
                var child2Value = (decimal)((1 - weight) * (double)value1 + weight * (double)value2);

                property.SetValue(child1Genome, child1Value);
                property.SetValue(child2Genome, child2Value);
            }
            else if (property.PropertyType == typeof(int))
            {
                var value1 = (int)property.GetValue(parent1.Genome);
                var value2 = (int)property.GetValue(parent2.Genome);

                var child1Value = (int)Math.Round(weight * value1 + (1 - weight) * value2);
                var child2Value = (int)Math.Round((1 - weight) * value1 + weight * value2);

                // Ensure positive values
                child1Value = Math.Max(0, child1Value);
                child2Value = Math.Max(0, child2Value);

                property.SetValue(child1Genome, child1Value);
                property.SetValue(child2Genome, child2Value);
            }
        }

        var child1 = new Individual(parent1, parent2, "ArithmeticCrossover") { Genome = child1Genome };
        var child2 = new Individual(parent1, parent2, "ArithmeticCrossover") { Genome = child2Genome };

        return (child1, child2);
    }

    /// <summary>
    /// Gaussian mutation - adds random noise from a normal distribution
    /// </summary>
    public static Individual GaussianMutation(Individual individual, double mutationRate, double sigma, Random random)
    {
        var mutatedIndividual = individual.Clone();
        mutatedIndividual.EvolutionMethod = "GaussianMutation";

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (random.NextDouble() < mutationRate)
            {
                if (property.PropertyType == typeof(decimal))
                {
                    var currentValue = (decimal)property.GetValue(mutatedIndividual.Genome);
                    var noise = (decimal)(GenerateGaussianRandom(random) * sigma);
                    var newValue = currentValue + noise;
                    
                    // Apply reasonable bounds
                    newValue = Math.Max(-1000m, Math.Min(1000m, newValue));
                    property.SetValue(mutatedIndividual.Genome, newValue);
                }
                else if (property.PropertyType == typeof(int))
                {
                    var currentValue = (int)property.GetValue(mutatedIndividual.Genome);
                    var noise = (int)Math.Round(GenerateGaussianRandom(random) * sigma);
                    var newValue = currentValue + noise;
                    
                    // Ensure positive values for int properties
                    newValue = Math.Max(1, Math.Min(10000, newValue));
                    property.SetValue(mutatedIndividual.Genome, newValue);
                }
            }
        }

        return mutatedIndividual;
    }

    /// <summary>
    /// Uniform random mutation - replaces gene values with random values
    /// </summary>
    public static Individual UniformMutation(Individual individual, double mutationRate, Random random)
    {
        var mutatedIndividual = individual.Clone();
        mutatedIndividual.EvolutionMethod = "UniformMutation";

        var properties = typeof(HeuristicWeights).GetProperties()
            .Where(p => p.CanWrite && (p.PropertyType == typeof(decimal) || p.PropertyType == typeof(int)));

        foreach (var property in properties)
        {
            if (random.NextDouble() < mutationRate)
            {
                if (property.PropertyType == typeof(decimal))
                {
                    var newValue = GetRandomDecimalForProperty(property.Name, random);
                    property.SetValue(mutatedIndividual.Genome, newValue);
                }
                else if (property.PropertyType == typeof(int))
                {
                    var newValue = GetRandomIntForProperty(property.Name, random);
                    property.SetValue(mutatedIndividual.Genome, newValue);
                }
            }
        }

        return mutatedIndividual;
    }

    /// <summary>
    /// Adaptive mutation - adjusts mutation rate based on population diversity
    /// </summary>
    public static Individual AdaptiveMutation(Individual individual, double baseMutationRate, 
        double populationDiversity, Random random)
    {
        // Increase mutation rate when diversity is low, decrease when high
        var adaptedRate = baseMutationRate * (2.0 - Math.Min(1.0, populationDiversity / 10.0));
        
        return GaussianMutation(individual, adaptedRate, 1.0, random);
    }

    /// <summary>
    /// Generates a random number from a standard normal distribution
    /// </summary>
    private static double GenerateGaussianRandom(Random random)
    {
        // Box-Muller transform
        if (_hasSpare)
        {
            _hasSpare = false;
            return Math.Sqrt(-2.0 * Math.Log(_u1)) * Math.Sin(2.0 * Math.PI * _u2);
        }

        _hasSpare = true;
        _u1 = random.NextDouble();
        _u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(_u1)) * Math.Cos(2.0 * Math.PI * _u2);
    }

    /// <summary>
    /// Gets a random decimal value for a specific property
    /// </summary>
    private static decimal GetRandomDecimalForProperty(string propertyName, Random random)
    {
        return propertyName.ToLower() switch
        {
            var name when name.Contains("penalty") => (decimal)(random.NextDouble() * -50.0 - 5.0),
            var name when name.Contains("bonus") => (decimal)(random.NextDouble() * 50.0 + 5.0),
            var name when name.Contains("weight") || name.Contains("factor") => (decimal)(random.NextDouble() * 20.0 - 10.0),
            var name when name.Contains("distance") => (decimal)(random.NextDouble() * 100.0),
            var name when name.Contains("ratio") || name.Contains("percent") => (decimal)(random.NextDouble() * 2.0),
            var name when name.Contains("divisor") => (decimal)(random.NextDouble() * 10.0 + 1.0),
            _ => (decimal)(random.NextDouble() * 20.0 - 10.0)
        };
    }

    /// <summary>
    /// Gets a random int value for a specific property
    /// </summary>
    private static int GetRandomIntForProperty(string propertyName, Random random)
    {
        return propertyName.ToLower() switch
        {
            var name when name.Contains("threshold") => random.Next(1, 100),
            var name when name.Contains("size") || name.Contains("window") => random.Next(3, 20),
            var name when name.Contains("interval") => random.Next(1, 50),
            var name when name.Contains("distance") => random.Next(1, 20),
            _ => random.Next(1, 100)
        };
    }
}

/// <summary>
/// Configuration for genetic algorithm parameters
/// </summary>
public class GeneticAlgorithmConfig
{
    public int PopulationSize { get; set; } = 20;
    public int TournamentSize { get; set; } = 3;
    public double CrossoverRate { get; set; } = 0.8;
    public double MutationRate { get; set; } = 0.1;
    public double BlendAlpha { get; set; } = 0.5;
    public double MutationSigma { get; set; } = 1.0;
    public int EliteCount { get; set; } = 2;
    public int MaxGenerations { get; set; } = 100;
    public double ConvergenceThreshold { get; set; } = 0.001;
    public int StagnationLimit { get; set; } = 20;
    public string SelectionMethod { get; set; } = "Tournament"; // Tournament, Roulette, Rank
    public string CrossoverMethod { get; set; } = "Blend"; // Uniform, Blend, Arithmetic
    public string MutationMethod { get; set; } = "Gaussian"; // Gaussian, Uniform, Adaptive
} 