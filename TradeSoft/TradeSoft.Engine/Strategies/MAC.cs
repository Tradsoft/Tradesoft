using System;
using System.Collections.Generic;
using System.Linq;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Strategies
{
    public sealed class MovingAverageCrossover : Handler
    {
        private readonly int _shortPeriod;
        private readonly int _longPeriod;
        private readonly double _lotSize;

        private readonly Queue<double> _shortWindow = new();
        private readonly Queue<double> _longWindow = new();

        private double? _prevShortMA;
        private double? _prevLongMA;

        private int _currentDirection = 0;

        private readonly double _epsilon = 0.00001;
        private const double SafetyStopLossPercent = 0.02;

        public MovingAverageCrossover(int shortPeriod = 20, int longPeriod = 50, double lot = 1.0)
        {
            if (longPeriod <= shortPeriod) throw new ArgumentException("Long period must be greater than Short period");

            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
            _lotSize = lot * 100000;
        }

        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar == null) return;
            var bar = context.GeneratedBar;

            double price = bar.Close;
            long ts = bar.Timestamp.Ticks;

            UpdateWindow(_shortWindow, price, _shortPeriod);
            UpdateWindow(_longWindow, price, _longPeriod);

            if (_shortWindow.Count < _shortPeriod || _longWindow.Count < _longPeriod) return;

            double shortMA = _shortWindow.Average();
            double longMA = _longWindow.Average();

            if (_prevShortMA.HasValue && _prevLongMA.HasValue)
            {
                double prevDiff = _prevShortMA.Value - _prevLongMA.Value;
                double currDiff = shortMA - longMA;

                bool bullishCross = (prevDiff <= 0 && currDiff > _epsilon);
                bool bearishCross = (prevDiff >= 0 && currDiff < -_epsilon);

                if (bullishCross && _currentDirection != 1)
                {
                    SendOrder(context, _lotSize, price, "BUY", shortMA, longMA, ts);
                    _currentDirection = 1;
                }
                else if (bearishCross && _currentDirection != -1)
                {
                    SendToTarget(context, -_lotSize, price, "SELL", shortMA, longMA, ts);
                    _currentDirection = -1;
                }
            }

            _prevShortMA = shortMA;
            _prevLongMA = longMA;
        }

        private void SendToTarget(TickContext context, double targetUnits, double price, string label, double sMa, double lMa, long ts)
        {
            double currentPos = (_currentDirection == 1) ? _lotSize : (_currentDirection == -1 ? -_lotSize : 0);
            double delta = targetUnits - currentPos;

            if (Math.Abs(delta) < 1) return;

            Side side = delta > 0 ? Side.Buy : Side.Sell;
            double qty = Math.Abs(delta);

            double slPrice;
            if (side == Side.Buy)
                slPrice = price * (1.0 - SafetyStopLossPercent); 
            else
                slPrice = price * (1.0 + SafetyStopLossPercent); 

            context.NewOrders.Add(new Order
            {
                Id = DateTime.UtcNow.Ticks,
                Timestamp = ts,
                Symbol = "EURUSD",
                Side = side,
                Quantity = qty,
                Type = OrderType.Market,
                Price = price,
                StopLoss = slPrice, 
                Comment = $"MA_Cross_{label}"
            });

            Console.WriteLine($"[MA] {label} | P={price:F2} | SL={slPrice:F2} | MA{_shortPeriod}={sMa:F2} MA{_longPeriod}={lMa:F2}");
        }

        private void SendOrder(TickContext context, double quantity, double price, string label, double sMa, double lMa, long ts)
        {
            SendToTarget(context, quantity, price, label, sMa, lMa, ts);
        }

        private static void UpdateWindow(Queue<double> window, double value, int maxSize)
        {
            window.Enqueue(value);
            if (window.Count > maxSize) window.Dequeue();
        }
    }
}