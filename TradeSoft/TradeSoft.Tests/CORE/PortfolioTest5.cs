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
            var portfolio = new Portfolio(startingCash: 10_000);

            var negativeQtyTrade = new Trade
            {
                OrderId = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.1000,
                Quantity = -1000,
                Commission = 2,
                Timestamp = DateTime.Now.Ticks
            };

            var negativePriceTrade = new Trade
            {
                OrderId = 2,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = -1.1000,
                Quantity = 1000,
                Commission = 2,
                Timestamp = DateTime.Now.Ticks
            };

            Assert.Throws<ArgumentException>(() => portfolio.OnTrade(negativeQtyTrade));
            Assert.Throws<ArgumentException>(() => portfolio.OnTrade(negativePriceTrade));

            Assert.Equal(10_000, portfolio.Balance);
            Assert.Empty(portfolio.Positions);
            Assert.Empty(portfolio.History);
            Assert.Equal(10_000, portfolio.Equity);
        }
    }
}
