﻿
using System;
using System.Collections.Generic;
using System.Linq;

namespace Splendor.Core
{
    internal static class Utility
    {
        private static readonly Random s_Rng = new Random();

        /// <summary>
        /// Mutates a list, reordering the contents at random.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = s_Rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static Dictionary<TokenColour,int> CreateEmptyTokenPool()
        {
            return Enum.GetValues(typeof(TokenColour))
                       .OfType<TokenColour>()
                       .ToDictionary(col => col, col => 0);
        }

        public static Dictionary<T, T2> CreateCopy<T,T2>(this IDictionary<T, T2> dictionary)
        {
            return dictionary.ToDictionary(d => d.Key, d => d.Value);
        }

        public static Dictionary<T, T2> CreateCopy<T, T2>(this IReadOnlyDictionary<T, T2> dictionary)
        {
            return dictionary.ToDictionary(d => d.Key, d => d.Value);
        }

        public static Dictionary<T, int> MergeWith<T>(this IDictionary<T, int> dictionary, IDictionary<T, int> other)
        {
            var newDict = dictionary.Keys.Union(other.Keys).ToDictionary(col => col, col => 0);
            foreach (var kvp in dictionary) newDict[kvp.Key] += kvp.Value;
            foreach (var kvp in other)      newDict[kvp.Key] += kvp.Value;
            return newDict;
        }

        public static Dictionary<TokenColour, int> CreatePurse(int black = 0, int blue = 0, int red = 0, int green = 0, int white = 0, int gold = 0)
        {
            return new Dictionary<TokenColour, int>
            {
                { TokenColour.Black, black },
                { TokenColour.Blue,  blue  },
                { TokenColour.Red,   red   },
                { TokenColour.Green, green },
                { TokenColour.White, white },
                { TokenColour.Gold,  gold  },
            };
        }
    }
}
