using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public sealed class MovingAverageCrossover : Handler
    {
        private readonly int _shortPeriod;
        private readonly int _longPeriod;
        private readonly double _lot;

        private readonly Queue<double> _shortWindow = new();
        private readonly Queue<double> _longWindow = new();

        private double? _prevShortMA;
        private double? _prevLongMA;
        private double _positionLots = 0.0;
        private readonly double _epsilon = 0.00005;

        public MovingAverageCrossover(int shortPeriod = 20, int longPeriod = 50, double lot = 0.01)
        {
            if (longPeriod <= shortPeriod) throw new ArgumentException("Long > Short required");
            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
            _lot = lot;
        }

        protected override void OnProcess(TickContext context)
        {
            // MODIF : On attend la bougie
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;

            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

            // Mise à jour des SMA
            UpdateWindow(_shortWindow, price, _shortPeriod);
            UpdateWindow(_longWindow, price, _longPeriod);

            if (_shortWindow.Count < _shortPeriod || _longWindow.Count < _longPeriod) return;

            double shortMA = Average(_shortWindow);
            double longMA = Average(_longWindow);

            if (_prevShortMA.HasValue && _prevLongMA.HasValue)
            {
                double prevDiff = _prevShortMA.Value - _prevLongMA.Value;
                double diff = shortMA - longMA;

                bool bullishCross = (prevDiff <= 0 && diff > +_epsilon);
                bool bearishCross = (prevDiff >= 0 && diff < -_epsilon);

                if (bullishCross)
                {
                    SendToTarget(context, +_lot, price, "BUY", shortMA, longMA, ts);
                }
                else if (bearishCross)
                {
                    // Sortie de position (Flat)
                    SendToTarget(context, 0.0, price, "EXIT", shortMA, longMA, ts);
                }
            }

            _prevShortMA = shortMA;
            _prevLongMA = longMA;
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double shortMA, double longMA, long ts)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12) return;

            Side side = delta > 0 ? Side.Buy : Side.Sell;
            context.NewOrders.Add(new Order
            {
                Id = DateTime.UtcNow.Ticks,
                Timestamp = ts,
                Side = side,
                Quantity = Math.Abs(delta),
                Type = OrderType.Market,
                Price = price
            });

            _positionLots = targetLots;
            Console.WriteLine($"[MA CROSS] {label} | MA{_shortPeriod}={shortMA:F5} MA{_longPeriod}={longMA:F5} | {new DateTime(ts):HH:mm}");
        }

        private static void UpdateWindow(Queue<double> window, double value, int maxSize)
        {
            window.Enqueue(value);
            if (window.Count > maxSize) window.Dequeue();
        }

        private static double Average(IEnumerable<double> values)
        {
            double sum = 0;
            int count = 0;
            foreach (var v in values) { sum += v; count++; }
            return count == 0 ? 0 : sum / count;
        }
    }
}