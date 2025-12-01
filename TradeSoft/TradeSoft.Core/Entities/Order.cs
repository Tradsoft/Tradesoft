using TradeSoft.Core.Enums;

namespace TradeSoft.Core.Entities
{
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } 
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public decimal Quantity { get; set; } 
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string RejectReason { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Side} {Quantity} {Symbol} @ {Type}";
        }
    }
}