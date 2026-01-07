namespace TradeSoft.Core.Domain
{
    public enum Side : byte { Buy, Sell }
    public enum OrderType : byte { Market, Limit }

    // On utilise une classe pour pouvoir la modifier (mutabilité) sans copie, 
    // mais on fera du "Pooling" plus tard pour éviter le GC.
    public class Order
    {
        public long Id { get; set; }
        public long Timestamp { get; set; }
        public Side Side { get; set; }
        public double Price { get; set; }     
        public double Quantity { get; set; }
        public OrderType Type { get; set; }

        public bool IsFilled { get; set; }

        public override string ToString() => $"{Side} {Quantity} @ {Price}";
    }
}