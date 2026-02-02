using System;
using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class TradeTest
    {
        [Fact]
        public void CreateTrade_WithValidData_ShouldInitializeCorrectly()
        {
            // Arrange
            long timestamp = DateTime.Now.Ticks;

            // Act
            var trade = new Trade
            {
                OrderId = 42,
                Timestamp = timestamp,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.1050,
                Quantity = 1000,
                Commission = 2.5
            };

            // Assert
            Assert.Equal(42, trade.OrderId);
            Assert.Equal(timestamp, trade.Timestamp);
            Assert.Equal("EURUSD", trade.Symbol);
            Assert.Equal(Side.Buy, trade.Side);
            Assert.Equal(1.1050, trade.Price);
            Assert.Equal(1000, trade.Quantity);
            Assert.Equal(2.5, trade.Commission);
        }
    }
}
