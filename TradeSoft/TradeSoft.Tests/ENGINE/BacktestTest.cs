using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestTest
    {
        [Fact]
        public void Backtest_WithValidTickAndTrades_ShouldUpdatePortfolioCorrectly()
        {
            double initialCash = 10_000;
            var portfolioManager = new PortfolioManager(initialCash);

            var tickData = new TickData(
                timestamp: DateTime.Now.Ticks,
                bid: 1.0990,
                ask: 1.1010,
                volume: 1000,
                flags: TickFlags.Trade
            );

            var tradesForThisTick = new List<Trade>
            {
                new Trade
                {
                    OrderId = 1,
                    Timestamp = tickData.Timestamp,
                    Symbol = "EURUSD",
                    Side = Side.Buy,
                    Price = 1.1000,
                    Quantity = 1000,
                    Commission = 2
                },
                new Trade
                {
                    OrderId = 2,
                    Timestamp = tickData.Timestamp,
                    Symbol = "EURUSD",
                    Side = Side.Sell,
                    Price = 1.1050,
                    Quantity = 1000,
                    Commission = 2
                }
            };

            var context = new TickContext();
            context.Reset(tickData);
            context.Trades.AddRange(tradesForThisTick);

            portfolioManager.ProcessTick(context);

            Assert.Equal(10_001, portfolioManager.CurrentPortfolio.Balance, 5);
            Assert.Empty(portfolioManager.CurrentPortfolio.Positions);

            Assert.Single(portfolioManager.CurrentPortfolio.History);
            var closedTrade = portfolioManager.CurrentPortfolio.History[0];
            Assert.Equal(5, closedTrade.PnL, 5);
            Assert.Equal(4, closedTrade.Commission, 5);

            Assert.Equal(portfolioManager.CurrentPortfolio.Equity, portfolioManager.PeakEquity);
            Assert.Contains(portfolioManager.CurrentPortfolio.Equity,
                portfolioManager.EquityCurve.ConvertAll(e => e.Equity));
        }
    }
}
