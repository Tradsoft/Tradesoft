using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
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
        private double _positionLots = 0.0;

        public RSI(int period = 14, double oversold = 30, double overbought = 70, double lot = 1.0)
        {
            if (period <= 1) throw new ArgumentOutOfRangeException(nameof(period));
            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _lot = lot;
        }

        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;

            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

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

                if (_initCount < _period) return;

                _avgGain = _sumGain / _period;
                _avgLoss = _sumLoss / _period;
                _initialized = true;

                _prevRsi = ComputeRsi(_avgGain, _avgLoss);
                return;
            }

            _avgGain = ((_avgGain * (_period - 1)) + gain) / _period;
            _avgLoss = ((_avgLoss * (_period - 1)) + loss) / _period;

            double rsi = ComputeRsi(_avgGain, _avgLoss);

            if (_prevRsi.HasValue)
            {
                bool buySignal = (_prevRsi.Value <= _oversold && rsi > _oversold);
                bool sellSignal = (_prevRsi.Value >= _overbought && rsi < _overbought);

                if (buySignal)
                {
                    SendToTarget(context, +_lot, price, "BUY", rsi, ts);
                }
                else if (sellSignal)
                {
                    SendToTarget(context, -_lot, price, "SELL", rsi, ts);
                }
            }

            _prevRsi = rsi;
        }

        private void SendToTarget(TickContext context, double targetLots, double price, string label, double rsi, long timestamp)
        {
            double delta = targetLots - _positionLots;
            if (Math.Abs(delta) < 1e-12) return;

            Side side = delta > 0 ? Side.Buy : Side.Sell;
            double qty = Math.Abs(delta);

            context.NewOrders.Add(new Order
            {
                Id = DateTime.UtcNow.Ticks,
                Timestamp = timestamp,
                Symbol = "EURUSD",
                Side = side,
                Quantity = qty,
                Type = OrderType.Market,
                Price = price
            });

            _positionLots = targetLots;
            Console.WriteLine($"[RSI] {label} | RSI={rsi:F2} | Target={targetLots} | {new DateTime(timestamp):HH:mm}");
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