
using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.Core;
using Splendor.Core.Actions;

using static System.Linq.Enumerable;
using static Splendor.Core.TokenColour;

namespace Splendor.AIs.Unofficial.Actions
{
    public sealed class BuyCardActionEnumerator : IActionEnumerator
    {
        public IEnumerable<IEnumerable<IAction>> GenerateValidActionVariations
            (GameState gameState)
        {
            if (gameState is null)
            {
                throw new ArgumentNullException(nameof(gameState));
            }

            var cards = gameState.Tiers
                .SelectMany(t => t.ColumnSlots.Values)
                .Where(c => c != null)
                .Concat(gameState.CurrentPlayer.ReservedCards);

            foreach (var card in cards)
            {
                yield return BuyPermutations(card, gameState.CurrentPlayer);
            }
        }

        private static IEnumerable<IAction> BuyPermutations(Card card,
                                                            Player player)
        {
            var costAfterBonuses = player.Bonuses.DeficitFor(card.Cost);

            var dontHave = player.Purse.DeficitFor(costAfterBonuses);

            if (dontHave.Sum > player.Purse.Gold) return new IAction[] {};

            var doHave = dontHave.DeficitFor(costAfterBonuses);

            var doHaveColours = doHave.Colours()
                               .SelectMany(c => Repeat(c, doHave[c]))
                               .ToArray();

            var extraGold = player.Purse.Gold - dontHave.Sum;

            return PaymentPermutations(doHaveColours, extraGold)
                  .Select(c => { Array.Sort(c); return c; })
                  .Distinct(new ColoursEqualityComparer())
                  .Select(c => c.Concat(Repeat(Gold, dontHave.Sum)))
                  .Select(c => new BuyCard(card, c.ToPool()));
        }

        private static IEnumerable<TokenColour[]> PaymentPermutations
            (TokenColour[] costColours, int goldCount)
        {
            for (var useGold = 0; useGold <= goldCount; ++useGold)
            {
                foreach (var indexes in costColours.IndexPermutations(useGold))
                {
                    var useColours = costColours.ToArray();

                    foreach (var index in indexes) useColours[index] = Gold;

                    yield return useColours;
                }
            }
        }
    }
}