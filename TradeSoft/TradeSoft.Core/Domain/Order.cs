namespace TradeSoft.Core.Domain
{
    public enum Side : byte { Buy, Sell }
    public enum OrderType : byte { Market, Limit, Stop }

    public class Order
    {
        public long Id { get; set; }
        public long Timestamp { get; set; }
        public Side Side { get; set; }
        public double Price { get; set; }
        public double Quantity { get; set; }
        public OrderType Type { get; set; }

        public bool IsFilled { get; set; }


        public double? StopLoss { get; set; }

        public double? TakeProfit { get; set; }

        public string? Comment { get; set; }

        public override string ToString()
        {
            string sl = StopLoss.HasValue ? $" SL:{StopLoss}" : "";
            string tp = TakeProfit.HasValue ? $" TP:{TakeProfit}" : "";
            return $"{Side} {Quantity} @ {Price}{sl}{tp}";
        }
    }
}