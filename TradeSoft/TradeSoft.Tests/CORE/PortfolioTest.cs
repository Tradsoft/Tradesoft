using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class PortfolioTest
    {
        [Fact]
        public void CreatePortfolio_WithValidInitialCash_ShouldInitializeCorrectly()
        {
            // Arrange
            double initialCash = 10_000;

            // Act
            var portfolio = new Portfolio(initialCash);

            // Assert
            Assert.NotNull(portfolio);
            Assert.Equal(initialCash, portfolio.InitialCash);
            Assert.Equal(initialCash, portfolio.Balance);
            Assert.Empty(portfolio.Positions);
            Assert.Empty(portfolio.History);
            Assert.Equal(initialCash, portfolio.Equity);
        }
    }
}
