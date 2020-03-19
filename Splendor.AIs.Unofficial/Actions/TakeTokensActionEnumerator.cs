
using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.Core;
using Splendor.Core.Actions;

using static System.Linq.Enumerable;
using static System.Math;

namespace Splendor.AIs.Unofficial.Actions
{
    public sealed class TakeTokensActionEnumerator : IActionEnumerator
    {
        public IEnumerable<IEnumerable<IAction>> GenerateValidActionVariations
            (GameState gameState)
        {
            if (gameState is null)
            {
                throw new ArgumentNullException(nameof(gameState));
            }

            if (gameState.Bank.Sum <= 0) return new IEnumerable<IAction>[] {};

            return TakeTwoVariations(gameState)
                  .Concat(TakeThreeVariations(gameState));
        }

        private static IEnumerable<IEnumerable<TakeTokens>> TakeTwoVariations
            (GameState gameState)
        {
            return gameState.Bank
                  .Colours(includeGold: false)
                  .Where(c => gameState.Bank[c] >= 4)
                  .Select(c => new Pool { [c] = 2 })
                  .Select(p => WithReturnVariations(p, gameState));
        }

        private static IEnumerable<IEnumerable<TakeTokens>> TakeThreeVariations
            (GameState gameState)
        {
            return gameState.Bank
                  .Colours(includeGold: false)
                  .ToArray()
                  .ItemPermutations(3)
                  .Select(l => l.ToPool())
                  .Select(p => WithReturnVariations(p, gameState));
        }

        private static IEnumerable<TakeTokens> WithReturnVariations
            (IPool toTake, GameState gameState)
        {
            TakeTokens Take(IPool toReturn) => new TakeTokens(toTake, toReturn);

            var totalTokens = gameState.CurrentPlayer.Purse.MergeWith(toTake);

            var returnCount = totalTokens.Sum - 10;

            if (returnCount <= 0) return new [] { Take(new Pool()) };

            return totalTokens
                  .Colours()
                  .OrderBy(c => c)
                  .SelectMany(c => Repeat(c, Min(totalTokens[c], returnCount)))
                  .ToArray()
                  .ItemPermutations(returnCount)
                  .Distinct(new ColoursEqualityComparer())
                  .Select(l => Take(l.ToPool()));
        }
    }
}