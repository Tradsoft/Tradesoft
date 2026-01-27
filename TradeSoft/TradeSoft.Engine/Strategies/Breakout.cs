using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public class BreakoutSupportResistance : Handler
    {
        private readonly int _lookback;
        private readonly double _lot;
        private readonly Queue<double> _window = new();
        private double _positionLots = 0.0;

        public BreakoutSupportResistance(int lookback = 20, double lot = 1.0)
        {
            _lookback = lookback;
            _lot = lot;
        }

        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;

            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

            if (_window.Count < _lookback)
            {
                _window.Enqueue(price);
                return;
            }

            double resistance = double.MinValue;
            double support = double.MaxValue;
            foreach (var p in _window)
            {
                if (p > resistance) resistance = p;
                if (p < support) support = p;
            }

            if (price > resistance)
            {
                SendToTarget(context, +_lot, price, "BUY (Breakout)", support, resistance, ts);
            }
            else if (price < support)
            {
                SendToTarget(context, -_lot, price, "SELL (Breakdown)", support, resistance, ts);
            }

            _window.Enqueue(price);
            if (_window.Count > _lookback) _window.Dequeue();
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double s, double r, long ts)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12) return;

            Side side = delta > 0 ? Side.Buy : Side.Sell;
            context.NewOrders.Add(new Order
            {
                Id = DateTime.UtcNow.Ticks,
                Timestamp = ts,
                Symbol = "EURUSD",
                Side = side,
                Quantity = Math.Abs(delta),
                Type = OrderType.Market,
                Price = price
            });

            _positionLots = targetLots;
            Console.WriteLine($"[Breakout] {label} | P={price:F5} Range=[{s:F5}, {r:F5}] | {new DateTime(ts):HH:mm}");
        }
    }
}