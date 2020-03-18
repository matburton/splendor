
using System;
using System.Collections.Generic;
using System.Linq;

using Splendor.AIs.Unofficial.Actions;
using Splendor.Core;
using Splendor.Core.Actions;
using Splendor.Core.AI;

namespace Splendor.AIs.Unofficial
{
    /// <summary>Picks a random valid action variation
    ///          from a random group of valid actions</summary>
    ///
    public sealed class BogoSpendorAi : ISpendorAi
    {
        public string Name { get; } = $"{Guid.NewGuid()}";

        public IAction ChooseAction(GameState gameState)
        {
            var actionVariations = m_ActionEnumerator
                                  .GenerateValidActionVariations(gameState)
                                  .ToList();

            while (actionVariations.Any())
            {
                var index = RandomIndex(actionVariations);

                var actions = actionVariations[index].ToArray();

                if (actions.Any()) return actions[RandomIndex(actions)];

                actionVariations.RemoveAt(index);
            }

            return new NoAction();
        }

        private int RandomIndex(IReadOnlyCollection<object> collection)
        {
            lock (m_Random) return m_Random.Next(collection.Count);
        }

        private readonly Random m_Random = new Random();

        private readonly IActionEnumerator m_ActionEnumerator =
            new CompositeActionEnumerator(new TakeTokensActionEnumerator(),
                                          new BuyCardActionEnumerator(),
                                          new ReserveCardActionEnumerator());
    }
}