using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    /// <summary>
    /// Breakout / Support-Resistance strategy
    /// 
    /// Signals:
    /// - BUY  when price breaks above resistance (max of previous N prices)
    /// - SELL when price breaks below support (min of previous N prices)
    /// 
    /// Position management:
    /// - target = +lot on BUY
    /// - target = -lot on SELL
    /// - no stacking in the same direction
    /// - reversal closes then flips
    /// </summary>
    public class BreakoutSupportResistance : Handler
    {
        private readonly int _lookback;
        private readonly double _lot;

        // Sliding window of previous prices
        private readonly Queue<double> _window = new();

        // Current position tracking
        private double _positionLots = 0.0;

        // Cooldown to avoid hyper-trading
        private long _lastSignalTimestamp = 0;
        private readonly long _cooldownTicks;

        public BreakoutSupportResistance(int lookback = 20, double lot = 1.0, TimeSpan? cooldown = null)
        {
            if (lookback <= 1) throw new ArgumentOutOfRangeException(nameof(lookback));
            if (lot <= 0) throw new ArgumentOutOfRangeException(nameof(lot));

            _lookback = lookback;
            _lot = lot;
            _cooldownTicks = (cooldown ?? TimeSpan.Zero).Ticks;
        }

        protected override void OnProcess(TickContext context)
        {
            double price = context.Tick.Median;
            long ts = context.Tick.Timestamp;

            // Si pas encore assez de données, remplir la fenêtre et attendre
            if (_window.Count < _lookback)
            {
                _window.Enqueue(price);
                return;
            }

            // Calcul support / résistance sur l'historique (excluant le prix courant)
            double resistance = double.MinValue;
            double support = double.MaxValue;
            foreach (var p in _window)
            {
                if (p > resistance) resistance = p;
                if (p < support) support = p;
            }

            // Vérifier cooldown
            if (_cooldownTicks > 0 && (ts - _lastSignalTimestamp) < _cooldownTicks)
            {
                // Mise à jour de la fenêtre
                _window.Enqueue(price);
                if (_window.Count > _lookback)
                    _window.Dequeue();
                return;
            }

            // Détection du breakout
            if (price > resistance)
            {
                SendToTarget(context, +_lot, price, "BUY", support, resistance);
                _lastSignalTimestamp = ts;
            }
            else if (price < support)
            {
                SendToTarget(context, -_lot, price, "SELL", support, resistance);
                _lastSignalTimestamp = ts;
            }

            // Mise à jour de la fenêtre après le signal
            _window.Enqueue(price);
            if (_window.Count > _lookback)
                _window.Dequeue();
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double support, double resistance)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12) return;

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

            Console.WriteLine($"[Breakout] {label} | Price={price:F5} | Support={support:F5} | Resistance={resistance:F5} | Target={targetLots:+0.##;-0.##;0} lots | {new DateTime(context.Tick.Timestamp)}");
        }
    }
}

