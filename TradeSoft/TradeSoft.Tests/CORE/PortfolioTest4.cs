using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest4
    {
        [Fact]
        public void OnTrade_WithEmptyTradeList_ShouldNotThrowException()
        {
            var portfolio = new Portfolio(startingCash: 10_000);

            var emptyTrades = new Trade[0];

            foreach (var trade in emptyTrades)
            {
                portfolio.OnTrade(trade);
            }

            portfolio.MarkToMarket(1.1000);

            Assert.Equal(10_000, portfolio.Balance);
            Assert.Empty(portfolio.Positions);
            Assert.Empty(portfolio.History);
            Assert.Equal(10_000, portfolio.Equity);
        }
    }
}
