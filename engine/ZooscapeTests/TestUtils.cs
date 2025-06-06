using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit.Abstractions;
using Zooscape.Domain.Enums;
using Zooscape.Domain.Interfaces;
using Zooscape.Domain.Models;

namespace ZooscapeTests;

public class TestUtils
{
    public static List<Guid> GenerateAnimalIds(int length = 4)
    {
        var animalIds = new List<Guid>();
        for (int i = 0; i < length; i++)
        {
            animalIds.Add(Guid.NewGuid());
        }

        return animalIds;
    }

    public static void QueueAnimalAction(IAnimal animal, BotAction direction)
    {
        animal.AddCommand(new AnimalCommand(animal.Id, direction));
    }

    public static void DrawHistogram(
        ITestOutputHelper testOutputHelper,
        List<double> values,
        double min,
        double max,
        int rows,
        int cols
    )
    {
        // Step 1: Initialize bins
        int[] bins = new int[cols];
        double binSize = (max - min) / cols;

        // Step 2: Populate bins
        foreach (double value in values)
        {
            if (value < min || value > max)
                continue;

            int binIndex = (int)((value - min) / binSize);
            if (binIndex == cols)
                binIndex--; // Handle edge case where value == max
            bins[binIndex]++;
        }

        // Step 3: Find max bin count to normalize
        int maxCount = bins.Max();
        if (maxCount == 0)
        {
            testOutputHelper.WriteLine("No data to display.");
            return;
        }

        // Step 4: Create grid and fill it
        for (int row = rows; row > 0; row--)
        {
            var sb = new StringBuilder();
            double threshold = (double)row / rows * maxCount;
            for (int col = 0; col < cols; col++)
            {
                if (bins[col] >= threshold)
                    sb.Append("*");
                else
                    sb.Append(" ");
            }
            testOutputHelper.WriteLine(sb.ToString());
        }

        // Step 5: Draw x-axis
        testOutputHelper.WriteLine(new string('-', cols));

        // Step 6: Draw labels
        testOutputHelper.WriteLine($"{min:F2}{new string(' ', cols - 10)}{max:F2}");
    }
}
