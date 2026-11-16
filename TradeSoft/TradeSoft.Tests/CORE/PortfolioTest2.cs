using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest2
    {
        [Fact]
        public void OnTrade_WithValidBuyTrade_ShouldCreatePositionAndUpdateBalance()
        {
            var portfolio = new Portfolio(startingCash: 10_000);

            var trade = new Trade
            {
                OrderId = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.1000,
                Quantity = 1000,
                Commission = 2,
                Timestamp = DateTime.Now.Ticks
            };

            portfolio.OnTrade(trade);

            Assert.Equal(9_998, portfolio.Balance);
            Assert.Single(portfolio.Positions);

            var position = portfolio.Positions[0];
            Assert.Equal("EURUSD", position.Symbol);
            Assert.Equal(Side.Buy, position.Side);
            Assert.Equal(1000, position.Quantity);
            Assert.Equal(1.1000, position.EntryPrice);
            Assert.Equal(2, position.EntryCommission);

            Assert.Empty(portfolio.History);
            Assert.Equal(portfolio.Balance, portfolio.Equity);
        }
    }
}
