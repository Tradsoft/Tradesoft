using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    /// <summary>
    /// Moving Average Crossover Strategy (SMA)
    ///
    /// BUY  : MA courte croise AU-DESSUS de MA longue
    /// EXIT : MA courte croise EN-DESSOUS de MA longue (long-only => pas de short)
    ///
    /// Sécurités intégrées :
    /// - 1 décision max par minute (anti sur-trading tick)
    /// - hystérésis (anti re-cross bruit)
    /// - position cible (+lot / 0)
    /// </summary>
    public sealed class MovingAverageCrossover : Handler
    {
        private readonly int _shortPeriod;
        private readonly int _longPeriod;
        private readonly double _lot;

        // Fenêtres SMA
        private readonly Queue<double> _shortWindow = new();
        private readonly Queue<double> _longWindow = new();

        // Précédentes valeurs MA
        private double? _prevShortMA;
        private double? _prevLongMA;

        // Position interne
        private double _positionLots = 0.0;

        // ---- Anti sur-trading : 1 décision / minute ----
        private long _lastDecisionBucket = -1;
        private readonly long _decisionBucketTicks = TimeSpan.FromMinutes(1).Ticks;

        // ---- Hystérésis (EURUSD) ----
        private readonly double _epsilon = 0.00005;

        public MovingAverageCrossover(
            int shortPeriod = 20,
            int longPeriod = 50,
            double lot = 0.01)
        {
            if (shortPeriod <= 0)
                throw new ArgumentOutOfRangeException(nameof(shortPeriod));

            if (longPeriod <= shortPeriod)
                throw new ArgumentException("Long period must be > short period.");

            if (lot <= 0)
                throw new ArgumentOutOfRangeException(nameof(lot));

            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
            _lot = lot;
        }

        protected override void OnProcess(TickContext context)
        {
            long ts = context.Tick.Timestamp;

            // 1) Décision max 1 fois / minute
            long bucket = ts / _decisionBucketTicks;
            if (bucket == _lastDecisionBucket)
                return;

            _lastDecisionBucket = bucket;

            double price = context.Tick.Median;


            // 2) Mise à jour des SMA
            UpdateWindow(_shortWindow, price, _shortPeriod);
            UpdateWindow(_longWindow, price, _longPeriod);

            if (_shortWindow.Count < _shortPeriod || _longWindow.Count < _longPeriod)
                return;

            double shortMA = Average(_shortWindow);
            double longMA = Average(_longWindow);


            // 3) Détection crossover
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
                    // Long-only: on sort (flat) au lieu de shorter
                    SendToTarget(context, 0.0, price, "EXIT", shortMA, longMA, ts);
                }
            }

            _prevShortMA = shortMA;
            _prevLongMA = longMA;
        }

        // Ordres vers position cible
        private void SendToTarget(
            TickContext context,
            double targetLots,
            double price,
            string label,
            double shortMA,
            double longMA,
            long ts)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12)
                return;

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

            Console.WriteLine(
                $"[MA CROSS] {label} | MA{_shortPeriod}={shortMA:F5} " +
                $"MA{_longPeriod}={longMA:F5} | Target={targetLots:+0.##;-0.##;0} | {new DateTime(ts)}"
            );
        }

        // Utils SMA
        private static void UpdateWindow(Queue<double> window, double value, int maxSize)
        {
            window.Enqueue(value);
            if (window.Count > maxSize)
                window.Dequeue();
        }

        private static double Average(IEnumerable<double> values)
        {
            double sum = 0;
            int count = 0;

            foreach (var v in values)
            {
                sum += v;
                count++;
            }

            return sum / count;
        }
    }
}
