namespace TradeSoft.Core.Domain
{
    public class Trade
    {
        public long OrderId { get; set; }
        public long Timestamp { get; set; } 
        public Side Side { get; set; }
        public double Price { get; set; }   // Prix d'exécution (peut différer du prix demandé aka Slippage)
        public double Quantity { get; set; }
        public double Commission { get; set; }

        public override string ToString()
			=> $"[EXECUTION] {Side} {Quantity} @ {Price} (Time: {new System.DateTime(Timestamp):yyyy-MM-dd HH:mm:ss.fff})";
    }
}