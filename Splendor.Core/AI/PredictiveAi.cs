
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Splendor.Core.Actions;

using static System.Environment;

namespace Splendor.Core.AI
{
    public sealed class PredictiveAi : ISpendorAi
    {
        /// <param name="createAi">
        /// Must be thread safe. The AI's returned don't need to
        /// be thread safe but they do need to be new instances</param>
        ///
        public PredictiveAi(Func<ISpendorAi> createAi)
        {
            m_CreateAi = createAi
                ?? throw new ArgumentNullException(nameof(createAi));
        }

        public string Name { get; set; } = nameof(PredictiveAi);

        public IAction ChooseAction(GameState gameState)
        {
            if (gameState is null)
            {
                throw new ArgumentNullException(nameof(gameState));
            }

            using var cancellationTokenSource = new CancellationTokenSource();

            using var possibleActionBuffer = new BlockingCollection<IAction>
                (ProcessorCount);

            using var winningActions = new BlockingCollection<IAction>();

            var cancellationToken = cancellationTokenSource.Token;

            var threads = new List<Thread>();

            void StartThread(Action threadStart)
            {
                var thread = new Thread(() =>
                {
                    try { threadStart(); }
                    catch (OperationCanceledException) {} // Why .NET why!
                });

                thread.Start();

                threads.Add(thread);
            }

            try
            {
                // ReSharper disable AccessToDisposedClosure
                // ReSharper disable ImplicitlyCapturedClosure
                StartThread(() =>
                {
                    foreach (var action in PossibleActions(gameState))
                    {
                        possibleActionBuffer.Add(action, cancellationToken);
                    }
                });

                foreach (var _ in Enumerable.Range(0, 1))//ProcessorCount))
                {
                    StartThread(() =>
                    {
                        while (true)
                        {
                            var action = possibleActionBuffer.Take
                                (cancellationToken);

                            if (IsWinningAction(action, gameState))
                            {
                                winningActions.Add(action, cancellationToken);
                            }
                        }
                    });
                }
                // ReSharper restore ImplicitlyCapturedClosure
                // ReSharper restore AccessToDisposedClosure

                var action = winningActions.TryTake(out var winningAction,
                                                    TimeSpan.FromSeconds(10))
                           ? winningAction
                           : new NoAction();

                return action is NoAction ? PossibleActions(gameState).First()
                                          : action;
            }
            finally
            {
                cancellationTokenSource.Cancel();

                threads.ForEach(t => t.Join());
            }
        }

        private static IEnumerable<IAction> PossibleActions(GameState gameState)
        {
            var me = gameState.CurrentPlayer;

            var cardsInGrid = gameState.Tiers
                                       .SelectMany(t => t.ColumnSlots)                                                
                                       .Select(s => s.Value)                                                
                                       .Where(card => card != null)
                                       .ToArray();

            // Build a card including reserved cards
            //
            var cardBuys = cardsInGrid
                .Concat(me.ReservedCards)
                .OrderByDescending(c => c.VictoryPoints)
                .Where(c => BuyCard.CanAffordCard(me, c))
                .Select(c => new BuyCard(c, BuyCard.CreateDefaultPaymentOrNull(me, c)));

            foreach (var cardBuy in cardBuys) yield return cardBuy;

            // Take 2 coins
            //
            if (me.Purse.Values.Sum() <= 8)
            {
                var twoCoinActions = gameState.TokensAvailable
                    .Where(p => p.Key != TokenColour.Gold)
                    .Where(p => p.Value >= 4)
                    .Select(p => TakeTokens(new Dictionary<TokenColour, int>
                                                    { { p.Key, 2 } }));

                foreach (var action in twoCoinActions) yield return action;
            }

            // Take 3 coins
            //
            if (   me.Purse.Values.Sum() <= 7
                && gameState.TokensAvailable.Count(p => p.Value > 0) >= 3)
            {
                var colours = gameState.TokensAvailable
                    .Where(p => p.Key != TokenColour.Gold)
                    .Where(p => p.Value > 0)
                    .Select(c => c.Key)
                    .ToArray();

                var twoColours = Permute(colours, colours, (a, b) => (a, b))
                                .ToArray();

                var threeColours = Permute
                    (twoColours, colours, (t, c) => new [] { t.a, t.b, c });

                var threeColourActions = threeColours
                    .Where(a => a.Distinct().Count() == 3)
                    .Select(a => TakeTokens(a.ToDictionary(c => c, _ => 1)));

                foreach (var action in threeColourActions) yield return action;
            }

            // Reserve a card
            //
            if (me.ReservedCards.Count < 3 && me.Purse.Values.Sum() < 10)
            {
                foreach (var card in cardsInGrid)
                    yield return new ReserveCard(card);
            }

            yield return new NoAction();
        }

        private static IEnumerable<TOut> Permute<TA, TB, TOut>
            (IEnumerable<TA> inA, 
             IEnumerable<TB> inB,
             Func<TA, TB, TOut> projection)
        {
            return inA.SelectMany(a => inB.Select(b => projection(a, b)));
        }

        private static TakeTokens TakeTokens
            (IReadOnlyDictionary<TokenColour, int> tokensToTake)
        {
            return new TakeTokens
                (tokensToTake.Concat(Utility.CreateEmptyTokenPool())
                             .GroupBy(p => p.Key)
                             .Select(g => g.First())
                             .ToDictionary(p => p.Key, p => p.Value));
        }

        private bool IsWinningAction(IAction action, GameState gameState)
        {
            if (action is NoAction) return true; // Unblock if at end

            var ourIndex = Array.IndexOf(gameState.Players,
                                         gameState.CurrentPlayer);

            var players =  gameState.Players.Select(p => p.Clone()).ToArray();

            gameState = gameState.CopyWith
                (gameState.TokensAvailable.ToDictionary(p => p.Key, p => p.Value),
                 gameState.Nobles.ToList(),
                 gameState.Tiers.Select(t => t.Clone()).ToArray(),
                 players);

            gameState.CurrentPlayer = gameState.Players[ourIndex];

            var game = new Game(gameState);

            game.CommitTurn(action);

            gameState = game.State;

            var ais = gameState.Players.Select(_ => m_CreateAi()).ToArray();

            var stuntDouble = ais[ourIndex];

            var aiGameRunner = new AiGameRunner
                (ais,
                 _ => {},
                 new ResumeGameInitialiser { GameState = gameState });

            var result = aiGameRunner.Run();

            return result[stuntDouble] >= result.Values.Max();
        }

        private sealed class ResumeGameInitialiser : IGameInitialiser
        {
            public GameState GameState { private get; set; }

            public GameState Create(int players) => GameState;
        }

        private readonly Func<ISpendorAi> m_CreateAi;
    }
}