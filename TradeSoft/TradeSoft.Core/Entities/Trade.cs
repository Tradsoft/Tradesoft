using TradeSoft.Core.Enums;

namespace TradeSoft.Core.Entities
{
    public class Trade
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public OrderSide Side { get; set; }
        public decimal Quantity { get; set; }
        public decimal ExecutionPrice { get; set; }
        public decimal Commission { get; set; }
        public decimal? PnL { get; set; }
    }
}