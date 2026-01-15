using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    /// <summary>
    /// Mean Reversion strategy using Moving Average + StdDev
    ///
    /// Signals:
    /// - BUY  when price goes below lower band
    /// - SELL when price goes above upper band
    ///
    /// Position management:
    /// - target = +lot on BUY
    /// - target = -lot on SELL
    /// - no stacking
    /// - reversal allowed
    /// </summary>
    public class MeanReversion : Handler
    {
        private readonly int _period;
        private readonly double _k;
        private readonly double _lot;

        private readonly Queue<double> _window = new();
        private double _sum;
        private double _sumSq;

        private double _positionLots = 0.0;

        private long _lastSignalTimestamp = 0;
        private readonly long _cooldownTicks;

        public MeanReversion(int period = 20, double k = 2.0, double lot = 1.0, TimeSpan? cooldown = null)
        {
            if (period <= 1) throw new ArgumentOutOfRangeException(nameof(period));
            if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));
            if (lot <= 0) throw new ArgumentOutOfRangeException(nameof(lot));

            _period = period;
            _k = k;
            _lot = lot;
            _cooldownTicks = (cooldown ?? TimeSpan.Zero).Ticks;
        }

        protected override void OnProcess(TickContext context)
        {
            double price = context.Tick.Median;
            long ts = context.Tick.Timestamp;

            // build rolling window
            _window.Enqueue(price);
            _sum += price;
            _sumSq += price * price;

            if (_window.Count > _period)
            {
                double removed = _window.Dequeue();
                _sum -= removed;
                _sumSq -= removed * removed;
            }

            if (_window.Count < _period)
                return;

            double mean = _sum / _period;
            double variance = (_sumSq / _period) - (mean * mean);
            double stdDev = Math.Sqrt(Math.Max(variance, 0.0));

            double upper = mean + _k * stdDev;
            double lower = mean - _k * stdDev;

            // Cooldown
            if (_cooldownTicks > 0 && (ts - _lastSignalTimestamp) < _cooldownTicks)
                return;

            if (price < lower)
            {
                SendToTarget(context, +_lot, price, "BUY", mean, upper, lower);
                _lastSignalTimestamp = ts;
            }
            else if (price > upper)
            {
                SendToTarget(context, -_lot, price, "SELL", mean, upper, lower);
                _lastSignalTimestamp = ts;
            }
        }

        private void SendToTarget(
            TickContext context,
            double targetLots,
            double price,
            string label,
            double mean,
            double upper,
            double lower)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12)
                return;

            Side side = delta > 0 ? Side.Buy : Side.Sell;
            double qty = Math.Abs(delta);

            context.NewOrders.Add(new Order
            {
                Id = DateTime.UtcNow.Ticks,
                Timestamp = context.Tick.Timestamp,
                Side = side,
                Quantity = qty,
                Type = OrderType.Market,
                Price = price
            });

            _positionLots = targetLots;

            Console.WriteLine(
                $"[MeanReversion] {label} | Price={price:F2} | Mean={mean:F2} | Bands=[{lower:F2},{upper:F2}] | Target={targetLots:+0.##;-0.##;0}"
            );
        }
    }
}

