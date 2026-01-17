using System.Collections.Generic;
using TradeSoft.Core.Domain;

namespace TradeSoft.Core.Context
{
    public class TickContext
    {
        public TickData Tick;

        public Bar? GeneratedBar;

        public readonly List<Order> NewOrders = new List<Order>(10);

        public readonly List<Trade> Trades = new List<Trade>(10);

        public bool IgnoreTick;

        /// <summary>
        /// Remet le contexte à zéro pour le prochain tick sans réallouer la mémoire.
        /// </summary>
        public void Reset(TickData tick)
        {
            Tick = tick;
            GeneratedBar = null;
            IgnoreTick = false;
            NewOrders.Clear();
            Trades.Clear();
        }
    }
}