using System;
using System.Collections.Generic;

namespace Domain.Utilities;

public class Helpers
{
    private static readonly Random _random = new Random();
    private static List<string> _usedNames = [];

    public static string GenerateRandomName()
    {
        // Define two lists of words
        string[] firstList = { "Big", "Lil'", "Stupid", "Stinky", "Fuzzy", "Squishy", "Phat" };
        string[] secondList = { "Ol'", "Red", "Rotten", "Wooden", "Russian", "Party" };
        string[] thirdList = { "Dog", "Bugger", "Robot", "Muppet", "Blob", "Crawler", "Snotter" };

        string newName = "";
        do
        {
            // Pick a random word from each list
            string firstWord = firstList[_random.Next(firstList.Length)];
            string secondWord = secondList[_random.Next(secondList.Length)];
            string thirdWord = thirdList[_random.Next(thirdList.Length)];

            // Combine the words to form a name
            newName = $"{firstWord} {secondWord} {thirdWord}";
        } while (_usedNames.Contains(newName));

        return newName;
    }
}
