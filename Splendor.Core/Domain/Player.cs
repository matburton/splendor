using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.Core;

namespace Splendor
{
    public class Player
    {
        public string Name { get; }

        internal Player(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Purse = Utility.CreatePurse();
            Nobles = new List<Noble>();
            ReservedCards = new List<Card>();
            CardsInPlay = new List<Card>();
        }

        public Player Clone() => new Player(Name)
            { Purse         = Purse.ToDictionary(p => p.Key, p => p.Value),
              ReservedCards = ReservedCards.ToList(),
              CardsInPlay   = CardsInPlay.ToList(),
              Nobles        = Nobles.ToList() };

        public IDictionary<TokenColour, int> Purse { get; private set; }
        public IList<Card> ReservedCards { get; private set; }
        public IList<Card> CardsInPlay { get; private set; }
        public IList<Noble> Nobles { get; private set; }

        public IDictionary<TokenColour, int> GetDiscount()
        {
            return Enum.GetValues(typeof(TokenColour))
                       .OfType<TokenColour>()
                       .ToDictionary(col => col, col => CardsInPlay.Count(c => c.BonusGiven == col));
        }

        public int VictoryPoints()
        {
            return CardsInPlay
                .Select(n => n.VictoryPoints)
                .Concat(Nobles.Select(c => c.VictoryPoints))
                .Sum();
        }
    }
}
