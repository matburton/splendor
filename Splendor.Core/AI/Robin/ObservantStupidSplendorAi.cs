﻿using Splendor.Core.Actions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Splendor.Core.AI
{
    public class ObservantStupidSplendorAi : ISpendorAi
    {
        public string Name { get; private set; }

        private AiOptions _options;

        public ObservantStupidSplendorAi(string name, AiOptions options = null)
        {
            Name = name;
            _options = options ?? new AiOptions();
        }

        public IAction ChooseAction(GameState gameState)
        {
            /* PRECALCULATIONS */

            var me = gameState.CurrentPlayer;
            var myScore = me.VictoryPoints;
            var otherPlayers = gameState.Players.Where(p => p != me).ToArray();

            bool CanBuy(Card card) => BuyCard.CanAffordCard(me, card);

            var coloursForNoble = GetColoursForCheapestNoble(gameState);

            var allFaceUpCards = gameState.Tiers.SelectMany(t => t.ColumnSlots)
                                                .Select(s => s.Value)
                                                .Where(card => card != null)
                                                .ToArray();

            var faceUpAndMyReserved = allFaceUpCards.Concat(me.ReservedCards).ToArray();

            var cardsICanBuy = faceUpAndMyReserved.Where(CanBuy);

            var myOrderedCardStudy = AnalyseCards(me.Budget, faceUpAndMyReserved, gameState, coloursForNoble)
                    .OrderBy(s => s.Repulsion)
                    .ThenByDescending(s => coloursForNoble.Contains(s.Card.BonusGiven)).ToArray(); // Costs us nothing

            var myTargetCard = myOrderedCardStudy.FirstOrDefault();

            CardFeasibilityStudy myNextTargetCard = null;
            if (_options.LooksAhead)
            {
                var targetDiscount = new Pool();
                targetDiscount[myTargetCard.Card.BonusGiven] = 1;
                var nextBudget = me.Budget.MergeWith(targetDiscount);
                myNextTargetCard = AnalyseCards(nextBudget, faceUpAndMyReserved.Except(new[] { myTargetCard.Card }).ToArray(), gameState, coloursForNoble)
                    .OrderBy(s => s.Repulsion)
                    .ThenByDescending(s => coloursForNoble.Contains(s.Card.BonusGiven)).ToArray()
                    .FirstOrDefault();
            }

            /* BEHAVIOUR */

            // Check to see if a player can win (including me)
            if (_options.IsTheiving) foreach (var player in gameState.Players.OrderByDescending(p => p == me))
                {
                    var score = player.VictoryPoints;

                    var nobleDangerMap = new List<TokenColour>();
                    foreach (var noble in gameState.Nobles)
                    {
                        var deficit = player.Bonuses.DeficitFor(noble.Cost);
                        if (deficit.Sum != 1) continue;
                        nobleDangerMap.Add(deficit.Colours().Single());
                    }

                    if (score < 10 && !nobleDangerMap.Any()) continue;

                    var riskCards = allFaceUpCards.Where(c => c.VictoryPoints + score + (nobleDangerMap.Contains(c.BonusGiven) ? 3 : 0) >= 15)
                                                  .Where(c => BuyCard.CanAffordCard(player, c))
                                                  .OrderByDescending(c => c.VictoryPoints + (nobleDangerMap.Contains(c.BonusGiven) ? 3 : 0))
                                                  .ToArray();

                    if (riskCards.Length == 0) continue;
                    if (player != me && riskCards.Length > 1) break; // We're screwed if he has more than one winning move.
                    var riskCard = riskCards.First();
                    if (cardsICanBuy.Contains(riskCard)) return new BuyCard(riskCard, BuyCard.CreateDefaultPaymentOrNull(me, riskCard));
                    if (player != me && me.ReservedCards.Count < 3) return new ReserveCard(riskCard);
                }

            // Buy a 2 or greater victory point card if possible
            foreach (var card in cardsICanBuy.Where(c => c.VictoryPoints > 1)
                                             .OrderByDescending(c => c.VictoryPoints))
            {
                var payment = BuyCard.CreateDefaultPaymentOrNull(me, card);
                return new BuyCard(card, payment);
            }

            // Buy favourite card if possible
            if (cardsICanBuy.Contains(myTargetCard.Card))
            {
                return new BuyCard(myTargetCard.Card, BuyCard.CreateDefaultPaymentOrNull(me, myTargetCard.Card));
            }

            // If I have 9 or more coins buy any reasonable card I can at all
            if (me.Purse.Sum > 8)
            {
                foreach (var study in myOrderedCardStudy.Where(s => s.Deficit.Sum == 0))
                {
                    var payment = BuyCard.CreateDefaultPaymentOrNull(me, study.Card);
                    return new BuyCard(study.Card, payment);
                }
            }

            // Take some coins - but if there are coins to return then favour reserving a card
            var takeTokens = GetTakeTokens(gameState, myTargetCard, myNextTargetCard);
            if (takeTokens != null && !takeTokens.TokensToReturn.Colours().Any()) return takeTokens;

            // Do a reserve
            if (!me.ReservedCards.Contains(myTargetCard.Card) && me.ReservedCards.Count < 3)
            {
                return new ReserveCard(myTargetCard.Card);
            }

            // Do a random reserve
            var action = ChooseRandomCardOrNull(gameState);
            if (action != null) return action;

            // do the give/take coins if possible
            return takeTokens ?? (IAction)new NoAction();
        }

        private TakeTokens GetTakeTokens(GameState gameState, CardFeasibilityStudy firstChoice, CardFeasibilityStudy secondChoice)
        {
            if (firstChoice == null) return null;

            var me = gameState.CurrentPlayer;

            var coloursAvailable = gameState.TokensAvailable.Colours().Where(col => col != TokenColour.Gold).ToList();
            var coinsCountICanTake = Math.Min(Math.Min(10 - me.Purse.Sum, 3), coloursAvailable.Count);

            if (coinsCountICanTake > 0)
            {
                if (firstChoice != null)
                {
                    var coloursNeeded = firstChoice.Deficit.Colours().ToList();
                    var coloursNeeded2 = secondChoice?.Deficit.Colours().ToList();
                    var ordering = coloursAvailable.OrderByDescending(col => coloursNeeded.Contains(col));
                    if (secondChoice != null) ordering = ordering.ThenByDescending(col => coloursNeeded2.Contains(col));
                    coloursAvailable = ordering.ToList();
                }
                else
                {
                    coloursAvailable.Shuffle();
                }
                var transaction = new Pool();

                if (_options.CanTakeTwo
                    && firstChoice.Deficit.Colours().Any(col => firstChoice.Deficit[col] >= 2)
                    && firstChoice.Deficit.Sum == 2
                    && coinsCountICanTake > 1)
                {
                    var neededColour = firstChoice.Deficit.Colours().First(col => firstChoice.Deficit[col] >= 2);
                    if (gameState.TokensAvailable[neededColour] > 3)
                    {
                        transaction[neededColour] = 2;
                        return new TakeTokens(transaction);
                    }
                }
                foreach (var colour in coloursAvailable.Take(coinsCountICanTake)) transaction[colour] = 1;
                return new TakeTokens(transaction);
            }

            // otherwise just swap a single coin towards what we need
            if (coloursAvailable.Count == 0) return null;
            var colourToTake = coloursAvailable.First();
            var colourToGiveBack = me.Purse.Colours()
                                           .OrderBy(c => c == TokenColour.Gold)
                                           .Where(c => c != colourToTake)
                                           .Cast<TokenColour?>()
                                           .FirstOrDefault();
            if (!colourToGiveBack.HasValue) return null;
            var take = new Pool();
            var give = new Pool();

            take[colourToTake] = 1;
            give[colourToGiveBack.Value] = 1;

            return new TakeTokens(take, give);
        }

        private TokenColour[] GetColoursForCheapestNoble(GameState gameState)
        {
            var me = gameState.CurrentPlayer;

            Noble cheapestNoble = null;
            int cheapestNobleDef = 99;
            foreach (var noble in gameState.Nobles)
            {
                var deficitColours = me.Purse.DeficitFor(noble.Cost);
                var deficitSum = me.Purse.DeficitFor(noble.Cost).Sum;

                if (deficitSum < cheapestNobleDef)
                {
                    cheapestNobleDef = deficitSum;
                    cheapestNoble = noble;
                }
            }
            return cheapestNoble?.Cost.Colours().ToArray() ?? new TokenColour[0];
        }

        private IEnumerable<CardFeasibilityStudy> AnalyseCards(IPool budget, Card[] cards, GameState state, TokenColour[] coloursForNoble)
        {
            var coloursNeeded = ColoursByUsefulness(budget, cards, state.CurrentPlayer);
            var bonuses = state.CurrentPlayer.Bonuses;
            var coloursWithoutFourOf = bonuses.Colours().Where(col => bonuses[col] < 4).ToArray();

            foreach (var card in cards)
            {
                var cost = card.Cost;
                if (cost == null) continue;
                var deficit = new Pool();
                int scarcity = 0;
                foreach (var colour in cost.Colours())
                {
                    deficit[colour] = Math.Max(0, cost[colour] - budget[colour]);
                    scarcity += Math.Max(0, deficit[colour] - state.TokensAvailable[colour]);
                }
                var repulsion = deficit.Sum + scarcity * 1;

                if (_options.AimsAtNobles 
                    && coloursWithoutFourOf.Contains(card.BonusGiven) 
                    && coloursForNoble.Contains(card.BonusGiven)) repulsion -= 1;

                if (coloursNeeded.Contains(card.BonusGiven)) repulsion -= 2;
                if (card.VictoryPoints > 0) repulsion -= 1;

                yield return new CardFeasibilityStudy { Deficit = deficit, Repulsion = repulsion, Card = card };
            }
        }

        private TokenColour[] ColoursByUsefulness(IPool budget, IEnumerable<Card> cards, Player player)
        {
            var colourChart = new Pool();

            int[] tiersToExamine = new[] { 1, 2, 3 };
            if (_options.PhasesGame)
            {
                var lateGame = player.VictoryPoints > 5;
                tiersToExamine = lateGame ? new[] { 2, 3 } : new[] { 1, 2 };
            }

            foreach (var card in cards.Where(c => tiersToExamine.Contains(c.Tier)))
            {
                var deficit = budget.DeficitFor(card.Cost);
                foreach (var colour in deficit.Colours()) colourChart[colour] += 1;
            }

            return colourChart.Colours()
                .Where(col => col != TokenColour.Gold)
                .OrderByDescending(col => colourChart[col])
                .ToArray();
        }

        private ReserveFaceDownCard ChooseRandomCardOrNull(GameState gameState)
        {
            var me = gameState.CurrentPlayer;
            if (me.ReservedCards.Count == 3) return null;

            var firstTier = gameState.Tiers.Single(t => t.Tier == 1);
            if (firstTier.HasFaceDownCardsRemaining)
            {
                return new ReserveFaceDownCard(1);
            }
            return null;
        }

        private class CardFeasibilityStudy
        {
            public int Repulsion { get; set; }
            public IPool Deficit { get; set; }
            public Card Card { get; set; }
        }
    }

    public class AiOptions
    {
        public bool IsTheiving { get; set; } = true;
        public bool LooksAhead { get; set; } = true;
        public bool AimsAtNobles { get; set; } = false;
        public bool CanTakeTwo { get; set; } = true;

        public bool PhasesGame { get; set; } = false;
    }
}
