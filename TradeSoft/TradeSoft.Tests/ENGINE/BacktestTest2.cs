using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestTest2
    {
        [Fact]
        public void Backtest_WithEmptyHistoricalData_ShouldNotFailAndKeepPortfolioIntact()
        {
            double initialCash = 10_000;
            var portfolioManager = new PortfolioManager(initialCash);

            var tickData = new TickData(
                timestamp: DateTime.Now.Ticks,
                bid: 1.1000,
                ask: 1.1020,
                volume: 1000,
                flags: TickFlags.None
            );

            var context = new TickContext();
            context.Reset(tickData);

            Exception? ex = Record.Exception(() => portfolioManager.ProcessTick(context));

            Assert.Null(ex);
            Assert.Equal(initialCash, portfolioManager.CurrentPortfolio.Balance);
            Assert.Empty(portfolioManager.CurrentPortfolio.Positions);
            Assert.Empty(portfolioManager.CurrentPortfolio.History);
            Assert.Equal(portfolioManager.CurrentPortfolio.Equity, portfolioManager.CurrentPortfolio.Balance);
        }
    }
}
