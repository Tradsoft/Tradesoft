using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    /// <summary>
    /// RSI (Wilder) strategy - classic definition + position management.
    ///
    /// Signals:
    /// - BUY  when RSI crosses UP oversold (exit oversold)
    /// - SELL when RSI crosses DOWN overbought (exit overbought)
    ///
    /// Position management (target position):
    /// - target = +lot on BUY signal
    /// - target = -lot on SELL signal
    /// - no stacking in the same direction
    /// - reversal closes then flips (order size = abs(target - current))
    /// </summary>
    public class RSI : Handler
    {
        private readonly int _period;
        private readonly double _oversold;
        private readonly double _overbought;
        private readonly double _lot;

        private bool _hasPrevPrice;
        private double _prevPrice;

        private bool _initialized;
        private int _initCount;
        private double _sumGain;
        private double _sumLoss;

        private double _avgGain;
        private double _avgLoss;

        private double? _prevRsi;

        // internal position tracking (assumes immediate fills in sim broker)
        // +lot => long, -lot => short
        private double _positionLots = 0.0;

        // optional: cooldown to avoid hyper-trading on noisy ticks
        private long _lastSignalTimestamp = 0;
        private readonly long _cooldownTicks; // TimeSpan ticks

        public RSI(int period = 14, double oversold = 30, double overbought = 70, double lot = 1.0, TimeSpan? cooldown = null)
        {
            if (period <= 1) throw new ArgumentOutOfRangeException(nameof(period), "RSI period must be > 1.");
            if (oversold <= 0 || oversold >= 100) throw new ArgumentOutOfRangeException(nameof(oversold), "Oversold must be in (0,100).");
            if (overbought <= 0 || overbought >= 100) throw new ArgumentOutOfRangeException(nameof(overbought), "Overbought must be in (0,100).");
            if (oversold >= overbought) throw new ArgumentException("Oversold must be < Overbought.");
            if (lot <= 0) throw new ArgumentOutOfRangeException(nameof(lot), "Lot must be > 0.");

            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _lot = lot;

            _cooldownTicks = (cooldown ?? TimeSpan.Zero).Ticks;
        }

        protected override void OnProcess(TickContext context)
        {
            double price = context.Tick.Median;
            long ts = context.Tick.Timestamp;

            if (!_hasPrevPrice)
            {
                _hasPrevPrice = true;
                _prevPrice = price;
                return;
            }

            double change = price - _prevPrice;
            _prevPrice = price;

            double gain = change > 0 ? change : 0.0;
            double loss = change < 0 ? -change : 0.0;

            if (!_initialized)
            {
                _sumGain += gain;
                _sumLoss += loss;
                _initCount++;

                if (_initCount < _period)
                    return;

                _avgGain = _sumGain / _period;
                _avgLoss = _sumLoss / _period;
                _initialized = true;

                double firstRsi = ComputeRsi(_avgGain, _avgLoss);
                _prevRsi = firstRsi;
                return;
            }

            _avgGain = ((_avgGain * (_period - 1)) + gain) / _period;
            _avgLoss = ((_avgLoss * (_period - 1)) + loss) / _period;

            double rsi = ComputeRsi(_avgGain, _avgLoss);

            if (_prevRsi.HasValue)
            {
                // Cooldown
                if (_cooldownTicks > 0 && (ts - _lastSignalTimestamp) < _cooldownTicks)
                {
                    _prevRsi = rsi;
                    return;
                }

                // Classic signals: exit zones
                bool buySignal = (_prevRsi.Value <= _oversold && rsi > _oversold);
                bool sellSignal = (_prevRsi.Value >= _overbought && rsi < _overbought);

                if (buySignal)
                {
                    // target position = +_lot
                    SendToTarget(context, targetLots: +_lot, price, "BUY", rsi);
                    _lastSignalTimestamp = ts;
                }
                else if (sellSignal)
                {
                    // target position = -_lot
                    SendToTarget(context, targetLots: -_lot, price, "SELL", rsi);
                    _lastSignalTimestamp = ts;
                }
            }

            _prevRsi = rsi;
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double rsi)
        {
            // delta = target - current
            double delta = targetLots - _positionLots;

            // already at target => no order
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

            // assume immediate fill
            _positionLots = targetLots;

            Console.WriteLine($"[RSI] {label} | RSI={rsi:F2} | Target={targetLots:+0.##;-0.##;0} lots | {new DateTime(context.Tick.Timestamp)}");
        }

        private static double ComputeRsi(double avgGain, double avgLoss)
        {
            if (avgGain == 0.0 && avgLoss == 0.0) return 50.0;
            if (avgLoss == 0.0) return 100.0;

            double rs = avgGain / avgLoss;
            return 100.0 - (100.0 / (1.0 + rs));
        }
    }
}
