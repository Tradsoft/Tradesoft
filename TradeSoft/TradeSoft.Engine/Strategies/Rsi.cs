using System;
using System.Diagnostics;
using System.Threading;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public class RSI : Handler
    {
        private static long _nextOrderId;
        private readonly int _period;
        private readonly double _oversold;
        private readonly double _overbought;
        private readonly double _lot;
        private readonly double _slPips;
        private readonly double _tpPips;

        private readonly double _tickSize;
        private readonly string _symbol;

        private bool _hasPrevPrice;
        private double _prevPrice;
        private bool _initialized;
        private int _initCount;
        private double _sumGain;
        private double _sumLoss;
        private double _avgGain;
        private double _avgLoss;
        private double? _prevRsi;
        private int _currentDirection = 0;

        public RSI(int period = 14, double oversold = 30, double overbought = 70, double lot = 1.0, double slPips = 50, double tpPips = 100, double tickSize = 0.0001, string symbol = "EURUSD")
        {
            if (period <= 1) throw new ArgumentOutOfRangeException(nameof(period));
            _period = period;
            _oversold = oversold;
            _overbought = overbought;
            _lot = lot;
            _slPips = slPips;
            _tpPips = tpPips;
            _tickSize = tickSize;
            _symbol = symbol;
        }

        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;
            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

            if (!_hasPrevPrice) { _hasPrevPrice = true; _prevPrice = price; return; }

            double change = price - _prevPrice;
            _prevPrice = price;

            double gain = change > 0 ? change : 0.0;
            double loss = change < 0 ? -change : 0.0;

            if (!_initialized)
            {
                _sumGain += gain; _sumLoss += loss; _initCount++;
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

                if (buySignal && _currentDirection != 1)
                {
                    SendOrder(context, Side.Buy, price, rsi, ts);
                    _currentDirection = 1;
                }
                else if (sellSignal && _currentDirection != -1)
                {
                    SendOrder(context, Side.Sell, price, rsi, ts);
                    _currentDirection = -1;
                }
            }
            _prevRsi = rsi;
        }

        private void SendOrder(TickContext context, Side side, double price, double rsi, long timestamp)
        {
            double slPrice, tpPrice;

            if (side == Side.Buy)
            {
                slPrice = price - (_slPips * _tickSize);
                tpPrice = price + (_tpPips * _tickSize);
            }
            else
            {
                slPrice = price + (_slPips * _tickSize);
                tpPrice = price - (_tpPips * _tickSize);
            }

            context.NewOrders.Add(new Order
            {
                Id = Interlocked.Increment(ref _nextOrderId),
                Timestamp = timestamp,
                Symbol = _symbol,
                Side = side,
                Quantity = _lot,
                Type = OrderType.Market,
                Price = price,
                StopLoss = slPrice,
                TakeProfit = tpPrice,
                Comment = $"RSI_{rsi:F1}"
            });

            Debug.WriteLine($"[RSI] {side} | Price={price:F5} | RSI={rsi:F1} | SL={slPrice:F5} TP={tpPrice:F5}");
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