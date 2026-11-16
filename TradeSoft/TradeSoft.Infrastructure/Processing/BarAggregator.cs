using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Infrastructure.Processing
{
    public class BarAggregator : Handler
    {
        private Bar? _currentBar;
        private DateTime _nextPeriodClose;
        private readonly TimeSpan _timeframe;
        private double _lastValidPrice = 0;
        private DateTime _lastTickTime = DateTime.MinValue;

        public BarAggregator(TimeSpan timeframe)
        {
            if (timeframe <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeframe), "Timeframe must be > 0.");

            _timeframe = timeframe;
        }

        protected override void OnProcess(TickContext context)
        {
            DateTime tickTime = new DateTime(context.Tick.Timestamp);
            double price = context.Tick.MidPrice;

            if (price <= 0.0001) return;

            bool massiveGap = (tickTime - _lastTickTime).TotalHours > 1;

            if (!massiveGap && _lastValidPrice > 0)
            {
                double variation = Math.Abs(price - _lastValidPrice) / _lastValidPrice;
                if (variation > 0.10)
                {
                    return;
                }
            }

            _lastValidPrice = price;
            _lastTickTime = tickTime;

            if (_currentBar == null)
            {
                StartNewBar(tickTime, price, context.Tick.Volume);
                return;
            }

            if (tickTime >= _nextPeriodClose)
            {
                context.GeneratedBar = _currentBar;
                StartNewBar(tickTime, price, context.Tick.Volume);
            }
            else
            {
                if (price > _currentBar.High) _currentBar.High = price;
                if (price < _currentBar.Low) _currentBar.Low = price;

                _currentBar.Close = price;
                _currentBar.Volume += context.Tick.Volume;
            }
        }

        private void StartNewBar(DateTime tickTime, double price, int volume)
        {
            long ticks = tickTime.Ticks;
            long periodTicks = _timeframe.Ticks;
            long roundedTicks = (ticks / periodTicks) * periodTicks;
            var barTime = new DateTime(roundedTicks);

            _currentBar = new Bar
            {
                Timestamp = barTime,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            };

            _nextPeriodClose = barTime.Add(_timeframe);
        }
    }
}