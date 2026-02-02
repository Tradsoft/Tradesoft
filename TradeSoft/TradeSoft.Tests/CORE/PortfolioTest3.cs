using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest3
    {
        [Fact]
        public void OnTrade_WithNullTrade_ShouldThrowException()
        {
            // Arrange
            var portfolio = new Portfolio(startingCash: 10_000);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
            {
                portfolio.OnTrade(null);
            });
        }
    }
}


