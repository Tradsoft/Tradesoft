using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest4

    {
        [Fact]
        public void OnTrade_WithEmptyTradeList_ShouldNotThrowException()
        {
            // Arrange
            var portfolio = new Portfolio(startingCash: 10_000);

            // Act
            var emptyTrades = new Trade[0]; // liste vide

            foreach (var trade in emptyTrades)
            {
                portfolio.OnTrade(trade);
            }

            // MarkToMarket sur portefeuille vide
            portfolio.MarkToMarket(1.1000);

            // Assert
            // Vérifie que le portefeuille est toujours valide
            Assert.Equal(10_000, portfolio.Balance);
            Assert.Empty(portfolio.Positions);
            Assert.Empty(portfolio.History);
            Assert.Equal(10_000, portfolio.Equity);
        }
    }
}
