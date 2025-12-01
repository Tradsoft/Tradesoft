namespace TradeSoft.Core.Entities
{
    public class MarketQuote
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Ask { get; set; }
        public decimal Bid { get; set; }
        public long Volume { get; set; }
        public decimal MidPrice => (Ask + Bid) / 2m;
        public decimal Spread => Ask - Bid;

        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} | {Symbol} | Bid:{Bid} Ask:{Ask}";
        }
    }
}