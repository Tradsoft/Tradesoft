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
                double slDistance = 0.0050; 
                double tpDistance = 0.0100; 

                var order = new Order
                {
                    Id = DateTime.Now.Ticks, 
                    Timestamp = context.Tick.Timestamp,
                    Symbol = "EURUSD",
                    Side = Side.Buy,

                    Quantity = 1.0,
                    Type = OrderType.Market,
                    Price = bar.Close,

                    StopLoss = bar.Close - slDistance,
                    TakeProfit = bar.Close + tpDistance,
                    Comment = "Random Strategy"
                };

                context.NewOrders.Add(order);

                Console.WriteLine($"[STRATEGY] Achat {bar.Timestamp:HH:mm} @ {bar.Close} | SL: {order.StopLoss:F5} TP: {order.TakeProfit:F5}");
            }
        }
    }
}