using System.Collections.Generic;
using System.Linq;

namespace Splendor
{
    public sealed class BoardTier
    {
        internal BoardTier(int tier, IEnumerable<Card> cards, int columns)
        {
            Tier = tier;
            FaceDownCards = new Queue<Card>(cards);
            ColumnSlots = new Dictionary<int, Card>();

            for(var i = 0; i < columns; i++)
            {
                if(FaceDownCards.Count > 0) ColumnSlots[i] = FaceDownCards.Dequeue();
            }
        }

        public BoardTier Clone() => new BoardTier(Tier, FaceDownCards, 0)
            { FaceDownCards = new Queue<Card>(FaceDownCards),
              ColumnSlots   = ColumnSlots.ToDictionary(p => p.Key, p => p.Value) };

        public int Tier { get; private set; }
        public Queue<Card> FaceDownCards { get; private set; }
        public IDictionary<int,Card> ColumnSlots { get; private set; }
    }
}
