using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestScenario8Tests
    {
        [Fact]
        public void ProcessTick_WithEmptyTrades_ShouldNotThrowException()
        {
            // Arrange
            double initialCash = 10_000;
            var portfolioManager = new PortfolioManager(initialCash);

            // TickData fictif
            var tickData = new TickData(
                timestamp: DateTime.Now.Ticks,
                bid: 1.1000,
                ask: 1.1020,
                volume: 1000,
                flags: TickFlags.None
            );

            // TickContext avec liste vide de trades
            var context = new TickContext();
            context.Reset(tickData);
            context.Trades.Clear(); // vide au lieu de null

            // Act & Assert
            var ex = Record.Exception(() => portfolioManager.ProcessTick(context));

            // Aucun exception ne doit être levée
            Assert.Null(ex);

            // Vérifie que rien n'a été ajouté dans l'historique
            Assert.Empty(portfolioManager.CurrentPortfolio.Positions);
            Assert.Empty(portfolioManager.TradeHistory);
        }
    }
}



