using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioScenario5Tests
    {
        [Fact]
        public void OnTrade_WithNegativeQuantityOrPrice_ShouldThrowArgumentException()
        {
            // Arrange
            var portfolio = new Portfolio(startingCash: 10_000);

            // Trade avec quantité négative
            var negativeQtyTrade = new Trade
            {
                OrderId = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.1000,
                Quantity = -1000, // valeur négative
                Commission = 2,
                Timestamp = DateTime.Now.Ticks
            };

            // Trade avec prix négatif
            var negativePriceTrade = new Trade
            {
                OrderId = 2,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = -1.1000, // valeur négative
                Quantity = 1000,
                Commission = 2,
                Timestamp = DateTime.Now.Ticks
            };

            // Act & Assert
            // Vérifie que le portfolio ne tolère pas de trade avec quantité négative
            Assert.Throws<ArgumentException>(() => portfolio.OnTrade(negativeQtyTrade));

            // Vérifie que le portfolio ne tolère pas de trade avec prix négatif
            Assert.Throws<ArgumentException>(() => portfolio.OnTrade(negativePriceTrade));

            // Vérifie que le portefeuille reste cohérent
            Assert.Equal(10_000, portfolio.Balance);
            Assert.Empty(portfolio.Positions);
            Assert.Empty(portfolio.History);
            Assert.Equal(10_000, portfolio.Equity);
        }
    }
}

