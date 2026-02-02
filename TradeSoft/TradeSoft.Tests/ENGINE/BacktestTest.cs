using System;
using System.Collections.Generic;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Context;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestTest
    {
        [Fact]
        public void Backtest_WithValidTickAndTrades_ShouldUpdatePortfolioCorrectly()
        {
            // Arrange
            double initialCash = 10_000;
            var portfolioManager = new PortfolioManager(initialCash);

            // Création d'un TickData
            var tickData = new TickData(
                timestamp: DateTime.Now.Ticks,
                bid: 1.0990,
                ask: 1.1010,
                volume: 1000,
                flags: TickFlags.Trade
            );

            // Liste de trades pour ce tick (BUY puis SELL pour simuler commission)
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

            // TickContext
            var context = new TickContext();
            context.Reset(tickData);
            context.Trades.AddRange(tradesForThisTick);

            // Act
            portfolioManager.ProcessTick(context);

            // Assert
            // Balance après BUY et SELL et commissions
            // BUY commission -2, SELL commission -2, PnL = (1.1050-1.1000)*1000 = 5
            // Balance finale = 10_000 - 2 - 2 + 5 = 10_001
            Assert.Equal(10_001, portfolioManager.CurrentPortfolio.Balance, 5);

            // Vérifie que toutes les positions sont fermées
            Assert.Empty(portfolioManager.CurrentPortfolio.Positions);

            // Vérifie l'historique des trades
            Assert.Single(portfolioManager.CurrentPortfolio.History);
            var closedTrade = portfolioManager.CurrentPortfolio.History[0];
            Assert.Equal(5, closedTrade.PnL, 5); // PnL correct

            // Vérifie PeakEquity et EquityCurve
            Assert.Equal(portfolioManager.CurrentPortfolio.Equity, portfolioManager.PeakEquity);
            Assert.Contains(portfolioManager.CurrentPortfolio.Equity,
                portfolioManager.EquityCurve.ConvertAll(e => e.Equity));
        }
    }
}
