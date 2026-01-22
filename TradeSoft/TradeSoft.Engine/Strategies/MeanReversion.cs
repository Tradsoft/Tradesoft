using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public class MeanReversion : Handler
    {
        private readonly int _period;
        private readonly double _k;
        private readonly double _lot;

        private readonly Queue<double> _window = new();
        private double _sum;
        private double _sumSq;
        private double _positionLots = 0.0;

        public MeanReversion(int period = 20, double k = 2.0, double lot = 1.0)
        {
            _period = period;
            _k = k;
            _lot = lot;
        }

        protected override void OnProcess(TickContext context)
        {
            // MODIF : On attend la bougie
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;

            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

            _window.Enqueue(price);
            _sum += price;
            _sumSq += price * price;

            if (_window.Count > _period)
            {
                double removed = _window.Dequeue();
                _sum -= removed;
                _sumSq -= removed * removed;
            }

            if (_window.Count < _period) return;

            double mean = _sum / _period;
            double variance = (_sumSq / _period) - (mean * mean);
            double stdDev = Math.Sqrt(Math.Max(variance, 0.0));

            double upper = mean + _k * stdDev;
            double lower = mean - _k * stdDev;

            // Logique simple : Achat en bas, Vente en haut
            if (price < lower)
            {
                SendToTarget(context, +_lot, price, "BUY", mean, upper, lower, ts);
            }
            else if (price > upper)
            {
                SendToTarget(context, -_lot, price, "SELL", mean, upper, lower, ts);
            }
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double mean, double upper, double lower, long ts)
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
            Console.WriteLine($"[MeanRev] {label} | P={price:F5} Bands=[{lower:F5}, {upper:F5}] | {new DateTime(ts):HH:mm}");
        }
    }
}