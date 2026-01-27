using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Management
{
    public class PortfolioManager : Handler
    {
        private readonly Portfolio _portfolio;
        private DateTime _lastEquityRecordTime = DateTime.MinValue;

        public double PeakEquity { get; private set; }
        public double CurrentPrice { get; private set; }
        public int OpenPositionCount => _portfolio.Positions.Count;

        public readonly List<(DateTime Time, double Equity)> EquityCurve = new();
        public readonly List<Trade> TradeHistory = new List<Trade>();

        public Portfolio CurrentPortfolio => _portfolio;

        public PortfolioManager(double initialCash)
        {
            _portfolio = new Portfolio(initialCash);
            PeakEquity = initialCash;
            EquityCurve.Add((DateTime.MinValue, initialCash));
        }

        protected override void OnProcess(TickContext context)
        {
            CurrentPrice = context.Tick.MidPrice;

            foreach (var trade in context.Trades)
            {
                _portfolio.OnTrade(trade);
                TradeHistory.Add(trade);
            }

            _portfolio.MarkToMarket(CurrentPrice);

            if (_portfolio.Equity > PeakEquity)
            {
                PeakEquity = _portfolio.Equity;
            }

            bool isNewBar = context.GeneratedBar != null;
            bool hasTrade = context.Trades.Count > 0;

            if (isNewBar || hasTrade)
            {
                var currentTime = new DateTime(context.Tick.Timestamp);

                if (currentTime > _lastEquityRecordTime)
                {
                    EquityCurve.Add((currentTime, _portfolio.Equity));
                    _lastEquityRecordTime = currentTime;
                }
            }
        }
    }
}