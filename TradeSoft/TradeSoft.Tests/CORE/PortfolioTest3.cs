using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest3
    {
        [Fact]
        public void OnTrade_WithNullTrade_ShouldThrowArgumentNullException()
        {
            var portfolio = new Portfolio(startingCash: 10_000);

            Assert.Throws<ArgumentNullException>(() =>
            {
                portfolio.OnTrade(null!);
            });
        }
    }
}
