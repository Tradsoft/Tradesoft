using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public class RandomStrategy : Handler
    {
        private Random _rnd = new Random();

        protected override void OnProcess(TickContext context)
        {

            if (context.GeneratedBar == null)
            {
                return;
            }

            var bar = context.GeneratedBar;

            if (_rnd.Next(0, 10) == 0)
            {
                var order = new Order
                {
                    Id = DateTime.Now.Ticks,
                    Timestamp = context.Tick.Timestamp,
                    Side = Side.Buy,
                    Quantity = 1.0,
                    Type = OrderType.Market,

                    Price = bar.Close
                };

                context.NewOrders.Add(order);
                Console.WriteLine($"[STRATEGY] Achat sur bougie {bar.Timestamp:HH:mm} à {bar.Close}");
            }
        }
    }
}