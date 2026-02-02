using System;
using System.Collections.Generic;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Context;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestTest2    {
        [Fact]
        public void Backtest_WithEmptyHistoricalData_ShouldNotFailAndKeepPortfolioIntact()
        {
            // Arrange
            double initialCash = 10_000;
            var portfolioManager = new PortfolioManager(initialCash);

            // TickContext vide (pas de trades)
            var tickData = new TickData(
                timestamp: DateTime.Now.Ticks,
                bid: 1.1000,
                ask: 1.1020,
                volume: 1000,
                flags: TickFlags.None
            );

            var context = new TickContext();
            context.Reset(tickData);
            // Pas de trades ajoutés → liste vide

            // Act
            Exception ex = Record.Exception(() => portfolioManager.ProcessTick(context));

            // Assert
            // Aucune exception levée
            Assert.Null(ex);

            // Portfolio inchangé
            Assert.Equal(initialCash, portfolioManager.CurrentPortfolio.Balance);
            Assert.Empty(portfolioManager.CurrentPortfolio.Positions);
            Assert.Empty(portfolioManager.CurrentPortfolio.History);

            // Equity = balance
            Assert.Equal(portfolioManager.CurrentPortfolio.Equity, portfolioManager.CurrentPortfolio.Balance);
        }
    }
}

