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
        private readonly TimeSpan _timeframe = TimeSpan.FromMinutes(1);

        public BarAggregator(TimeSpan timeframe)
        {
            _timeframe = timeframe;
        }

        protected override void OnProcess(TickContext context)
        {
            DateTime tickTime = new DateTime(context.Tick.Timestamp);
            double price = context.Tick.MidPrice; 

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
            var barTime = new DateTime(tickTime.Year, tickTime.Month, tickTime.Day,
                                     tickTime.Hour, tickTime.Minute, 0);

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