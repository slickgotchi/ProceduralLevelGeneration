using System.Collections.Generic;
using UnityEngine;

public static class ListExtensions
{
    /// <summary>
    /// Shuffles the elements of a List<T> using the Fisher-Yates (Knuth) algorithm.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to shuffle.</param>
    public static void Shuffle<T>(this List<T> list)
    {
        if (list == null || list.Count <= 1)
            return; // No need to shuffle empty or single-element lists

        for (int i = list.Count - 1; i > 0; i--)
        {
            // Generate a random index between 0 and i (inclusive)
            int j = Random.Range(0, i + 1);

            // Swap elements at indices i and j
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}