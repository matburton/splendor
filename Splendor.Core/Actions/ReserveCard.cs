﻿using Splendor.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Splendor.Core.Actions
{
    public class ReserveCard : IAction
    {
        public Card Card { get; }
        TokenColour? ColourToReturnIfMaxCoins { get; }

        public ReserveCard(Card card, TokenColour? colourToReturnIfMaxCoins = null)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));

            ColourToReturnIfMaxCoins = colourToReturnIfMaxCoins;
        }

        public override string ToString()
        {
            return $"Reserved {Card}";
        }

        public GameState Execute(GameState gameState)
        {
            var player = gameState.CurrentPlayer;

            if (player.ReservedCards.Count > 3)
            {
                throw new RulesViolationException("You can't reserve more than three cards.");
            }

            var tier = gameState.Tiers.SingleOrDefault(t => t.ColumnSlots.Values.Contains(Card));

            if (tier == null)
            {
                throw new RulesViolationException("That card isn't on the board.");
            }

            var nextTiers = new List<BoardTier>(gameState.Tiers);
            var nextTier = gameState.Tiers.Single(t => t.Tier == Card.Tier);
            nextTiers.Remove(nextTier);
            nextTiers.Add(nextTier.Clone(withCardTaken: Card));
            var playerCardsInPlay = new List<Card>(player.CardsInPlay);
            var playerReserved = new List<Card>(player.ReservedCards);
            var playerPurse = player.Purse.CreateCopy();
            var nextTokensAvailable = gameState.TokensAvailable.CreateCopy();

            var index = tier.ColumnSlots.Single(s => s.Value == Card).Key;

            if (gameState.TokensAvailable[TokenColour.Gold] > 1)
            {
                if (player.Purse.Values.Sum() >= 10)
                {
                    var colourToReturn = ColourToReturnIfMaxCoins.HasValue
                        ? ColourToReturnIfMaxCoins.Value
                        : player.Purse.First(kvp => kvp.Key != TokenColour.Gold && kvp.Value > 0).Key;

                    if (player.Purse[colourToReturn] < 1 && ColourToReturnIfMaxCoins != TokenColour.Gold)
                    {
                        throw new RulesViolationException("You can't give back a coin you don't have.");
                    }

                    playerPurse[colourToReturn]--;
                    nextTokensAvailable[colourToReturn]++;
                }

                nextTokensAvailable[TokenColour.Gold]--;
                playerPurse[TokenColour.Gold]++;
            }

            var nextPlayers = new List<Player>();
            foreach (var p in gameState.Players) if (p.Name == player.Name)
                    nextPlayers.Add(player.Clone(playerPurse, playerReserved, playerCardsInPlay));
                else nextPlayers.Add(p);
                
            return gameState.Clone(nextTokensAvailable, withTiers: nextTiers, withPlayers: nextPlayers);
        }
    }
}
