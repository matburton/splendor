﻿
using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.Core.Domain;

namespace Splendor.Core.Actions
{
    /// <summary>
    /// Accepts either a reserved card or one from the face-up cards.
    /// </summary>
    public class BuyCard : IAction
    {
        public Card Card { get; }
        public IReadOnlyDictionary<TokenColour, int> Payment { get; }

        public BuyCard(Card card, IReadOnlyDictionary<TokenColour, int> payment)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            Payment = payment ?? throw new ArgumentNullException(nameof(payment));
        }

        public override string ToString()
        {
            var costs = Payment?.Where(c => c.Value > 0).Select(kvp => $"{kvp.Value} {kvp.Key}").ToList();
            if (costs?.Count() == 0) return $"Buying [{Card}] for free.";
            return costs == null ? $"Buying {Card}" : $"Buying [{Card}] with {string.Join(", ", costs)}";
        }

        public GameState Execute(GameState gameState)
        {
            var player = gameState.CurrentPlayer;

            // Validate 
            if(!SuppliedPaymentIsValid(gameState)) throw new RulesViolationException("You can't afford this card with the supplied payment."); ;
            if(!VerifyCardIsInHandOrBoard(player, gameState, Card)) throw new RulesViolationException("That card isn't on the board or in hand.");

            var tier = gameState.Tiers.SingleOrDefault(t => t.ColumnSlots.Values.Contains(Card));

            // Perform transaction
            //  - Card
            if (tier != null)
            {
                var index = tier.ColumnSlots.Single(s => s.Value == Card).Key;
                tier.ColumnSlots[index] = tier.FaceDownCards.Count > 0 ? tier.FaceDownCards.Dequeue() : null;
            }
            else
            {
                player.ReservedCards.Remove(Card);
            }
            player.CardsInPlay.Add(Card);

            //  - Payment
            foreach(var colour in Payment.Keys)
            {
                player.Purse[colour] -= Payment[colour];
                gameState.TokensAvailable[colour] += Payment[colour];
            }

            return gameState;
        }

        public static bool CanAffordCard(Player player, Card card)
        {
            return CreateDefaultPaymentOrNull(player, card) != null;
        }

        public static Dictionary<TokenColour, int> CreateDefaultPaymentOrNull(Player player, Card card)
        {
            var payment = Utility.CreateEmptyTokenPool();

            var available = player.Purse.CreateCopy();
            var discount = player.GetDiscount();
            var costRemaining = card.Cost.CreateCopy();

            foreach (var colour in discount.Keys)
            {
                costRemaining[colour] = Math.Max(costRemaining[colour] - discount[colour], 0);
            }

            foreach (var colour in costRemaining.Keys)
            {
                if (costRemaining[colour] < 1) continue;
                if (costRemaining[colour] > available[colour] + available[TokenColour.Gold])
                {
                    return null;
                }

                if (costRemaining[colour] < available[colour])
                {
                    payment[colour] = costRemaining[colour];
                }
                else
                {
                    var goldNeeded = costRemaining[colour] - available[colour];
                    payment[colour] = available[colour];

                    payment[TokenColour.Gold] += goldNeeded;
                    available[TokenColour.Gold] -= goldNeeded;
                }
            }

            return payment;
        }

        private static bool VerifyCardIsInHandOrBoard(Player player, GameState gameState, Card card)
        {
            if (player.ReservedCards.Contains(card)) return true;
            var tier = gameState.Tiers.SingleOrDefault(t => t.ColumnSlots.Values.Contains(card));
            return tier != null;
        }

        private bool SuppliedPaymentIsValid(GameState gameState)
        {
            var available = gameState.CurrentPlayer.Purse.CreateCopy();
            var costRemaining = Card.Cost.CreateCopy();
            var discount = gameState.CurrentPlayer.GetDiscount();

            foreach (var colour in discount.Keys)
            {
                costRemaining[colour] = Math.Min(costRemaining[colour] - discount[colour], 0);
            }

            foreach (var colour in costRemaining.Keys)
            {
                if (costRemaining[colour] < 1) continue;
                if (costRemaining[colour] > available[colour] + available[TokenColour.Gold])
                {
                    return false;                    
                }

                if (costRemaining[colour] > available[colour])
                {
                    available[TokenColour.Gold] -= costRemaining[colour] - available[colour];
                }
            }
            return true;
        }
    }
}
