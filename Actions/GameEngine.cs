﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Splendor.Core.Actions
{
    /// <summary>
    /// Logic for game rules other than initialisation
    /// </summary>
    public class GameEngine : IGameEngine
    {
        public GameState GameState { get; }
        public bool IsGameFinished { get; private set; }

        public GameEngine(GameState game)
        {
            GameState = game ?? throw new ArgumentNullException(nameof(game));
        }

        public void CommitTurn()
        {
            IsGameFinished = 
                GameState.Players.Last() == GameState.CurrentPlayer
                && GameState.Players.Any(p => p.VictoryPoints() >= 15);

            var nextIndex = (Array.IndexOf(GameState.Players, GameState.CurrentPlayer) + 1)
                % GameState.Players.Length;
            GameState.CurrentPlayer = GameState.Players[nextIndex];
        }
    }

    public interface IGameEngine
    {
        bool IsGameFinished { get; }
        GameState GameState { get; }
        void CommitTurn();
    }
}
