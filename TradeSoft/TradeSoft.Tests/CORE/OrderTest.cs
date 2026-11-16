using TradeSoft.Core.Domain;
using Xunit;

namespace TradeSoft.Tests.CORE
{
    public class OrderTest
    {
        [Fact]
        public void CreateOrder_WithValidData_ShouldCreateOrderCorrectly()
        {
            var order = new Order
            {
                Id = 1,
                Timestamp = 1700000000,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.085,
                Quantity = 1000,
                Type = OrderType.Market,
                IsFilled = false,
                StopLoss = 1.080,
                TakeProfit = 1.095,
                Comment = "Test order"
            };

            Assert.NotNull(order);
            Assert.Equal(1, order.Id);
            Assert.Equal("EURUSD", order.Symbol);
            Assert.Equal(Side.Buy, order.Side);
            Assert.Equal(1.085, order.Price);
            Assert.Equal(1000, order.Quantity);
            Assert.Equal(OrderType.Market, order.Type);
            Assert.False(order.IsFilled);
            Assert.Equal(1.080, order.StopLoss);
            Assert.Equal(1.095, order.TakeProfit);
            Assert.Equal("Test order", order.Comment);
        }
    }
}
