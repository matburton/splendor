
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
                (ProcessorCount * 2);

            using var winningActions = new BlockingCollection<IAction>();

            var cancellationToken = cancellationTokenSource.Token;

            var threads = new List<Thread>();

            void StartThread(Action threadStart)
            {
                var thread = new Thread(() => threadStart());

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

                        if (cancellationToken.IsCancellationRequested) return;
                    }
                });

                foreach (var _ in Enumerable.Range(0, ProcessorCount))
                {
                    StartThread(() =>
                    {
                        try
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
                        }
                        catch (OperationCanceledException) {} // Why .NET why!
                    });
                }
                // ReSharper restore ImplicitlyCapturedClosure
                // ReSharper restore AccessToDisposedClosure

                return winningActions.TryTake(out var winningAction,
                                              TimeSpan.FromSeconds(1))
                     ? winningAction
                     : new NoAction();
            }
            finally
            {
                cancellationTokenSource.Cancel();

                threads.ForEach(t => t.Join());
            }
        }

        private static IEnumerable<IAction> PossibleActions(GameState gameState)
        {
            yield return new NoAction();
        }

        private bool IsWinningAction(IAction action, GameState gameState)
        {
            var ourIndex = Array.IndexOf(gameState.Players,
                                         gameState.CurrentPlayer);

            var players =  gameState.Players.Select(p => p).ToArray(); // TODO: Clone

            gameState = gameState.CopyWith(players: players);

            gameState.CurrentPlayer = gameState.Players[ourIndex];

            gameState = action.Execute(gameState);

            var ais = gameState.Players.Select(_ => m_CreateAi()).ToArray();

            var stuntDouble = ais[ourIndex];

            var aiGameRunner = new AiGameRunner
                (ais,
                 _ => {},
                 new ResumeGameInitialiser { GameState = gameState });

            var result = aiGameRunner.Run();

            return result[stuntDouble] > result.Values.Max();
        }

        private sealed class ResumeGameInitialiser : IGameInitialiser
        {
            public GameState GameState { private get; set; }

            public GameState Create(int players) => GameState;
        }

        private readonly Func<ISpendorAi> m_CreateAi;
    }
}