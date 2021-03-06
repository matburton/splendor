﻿
using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.Core;
using Splendor.Core.Actions;

namespace Splendor.AIs.Unofficial.Actions
{
    public sealed class ReserveCardActionEnumerator : IActionEnumerator
    {
        public IEnumerable<IEnumerable<IAction>> GenerateValidActionVariations
            (GameState gameState)
        {
            if (gameState is null)
            {
                throw new ArgumentNullException(nameof(gameState));
            }

            if (gameState.CurrentPlayer.ReservedCards.Count >= 3)
            {
                yield break;
            }

            var tokenColours = gameState.CurrentPlayer.Purse.Sum <= 9 ? null
                             : gameState.CurrentPlayer.Purse.Colours()
                                        .Union(new [] { TokenColour.Gold })
                                        .ToArray();

            var tiers = gameState.Tiers.Where(t => t.HasFaceDownCardsRemaining);

            foreach (var tier in tiers)
            {
                yield return WithReturnColours
                    (tokenColours, c => new ReserveFaceDownCard(tier.Tier, c));
            }

            var cards = gameState.Tiers.SelectMany(t => t.ColumnSlots.Values);

            foreach (var card in cards.Where(c => c != null))
            {
                yield return WithReturnColours
                    (tokenColours, c => new ReserveCard(card, c));
            }
        }

        private static IEnumerable<IAction> WithReturnColours
            (IEnumerable<TokenColour> colours,
             Func<TokenColour?, IAction> toAction)
        {
            if (colours is null)
            {
                yield return toAction(null);

                yield break;
            }

            foreach (var colour in colours)
            {
                yield return toAction(colour);
            }
        }
    }
}