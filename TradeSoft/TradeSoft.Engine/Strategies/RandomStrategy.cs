using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public class RandomStrategy : Handler
    {
        private int _counter = 0;

        protected override void OnProcess(TickContext context)
        {
            _counter++;

            if (_counter >= 100000)
            {
                // Création d'un ordre
                var order = new Order
                {
                    Id = DateTime.Now.Ticks, 
                    Timestamp = context.Tick.Timestamp,
                    Side = Side.Buy,
                    Quantity = 1.0,
                    Type = OrderType.Market,
                    Price = context.Tick.Median 
                };

                context.NewOrders.Add(order);

                _counter = 0;

                Console.WriteLine($"[STRATEGY] Signal d'achat généré à {new DateTime(context.Tick.Timestamp)}");
            }
        }
    }
}