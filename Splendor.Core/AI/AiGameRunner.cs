using Splendor.Core.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Splendor.Core.AI
{
    public class AiGameRunner
    {
        private readonly Dictionary<Player, ISpendorAi> _playerMap;
        private readonly IGame _game;

        private readonly Action<string> m_Log;

        public AiGameRunner(IEnumerable<ISpendorAi> players,
                            Action<string> log,
                            IGameInitialiser gameInitialiser = null)
        {
            gameInitialiser ??= new DefaultGameInitialiser(new DefaultCards());

            var ais = players.ToArray();
            var state = gameInitialiser.Create(players: ais.Length);
            _playerMap = new Dictionary<Player, ISpendorAi>();
            for(int i = 0; i < state.Players.Length; i++)
            {
                _playerMap[state.Players[i]] = ais[i];
            }
            _game = new Game(state);
            m_Log = log ?? new Action<string>(s => { });
        }

        public Dictionary<ISpendorAi, int> Run()
        {
            int playersPassed = 0;
            while (!_game.IsGameFinished && playersPassed < _game.State.Players.Length)
            {
                var turnPlayer = _game.State.CurrentPlayer;
                var turnAi = _playerMap[turnPlayer];
                var action = turnAi.ChooseAction(_game.State);
                if (action is NoAction) playersPassed++; else playersPassed = 0;
                _game.CommitTurn(action);
                m_Log($"{turnAi.Name} (Bank:{turnPlayer.Purse.Values.Sum()}), {action}");
            }

            m_Log($"**** Game over after {_game.RoundsCompleted} rounds. Winner: " + _playerMap[_game.TopPlayer].Name);

            var results = new Dictionary<ISpendorAi, int>();
            for (int i = 0; i < _game.State.Players.Length; i++)
            {
                Player player = _game.State.Players[i];
                var ai = _playerMap[player];
                var score = player.VictoryPoints();
                var s = score == 1 ? "" : "s";
                var nobles = player.Nobles.Count();
                var ns = nobles == 1 ? "" : "s";
                var nobleNames = nobles > 0 ? ": " + string.Join(", ", player.Nobles.Select(n => n.Name)) : "";
                var bonuses = string.Join(", ", player.GetDiscount().Where(kvp => kvp.Key != TokenColour.Gold)
                                                                    .Select(kvp=> $"{kvp.Value} {kvp.Key}"));
                m_Log($"{ai.Name} — {score} point{s} ({nobles} noble{ns}{nobleNames}) (Bonuses {bonuses})");
                results.Add(ai, score);
            }
            return results;
        }
    }
}
