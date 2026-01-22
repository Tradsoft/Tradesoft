using System;

namespace TradeSoft.Core.Domain
{
    public class ClosedTrade
    {
        public string Symbol { get; set; } = string.Empty;
        public Side Direction { get; set; } 
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double Quantity { get; set; }
        public double PnL { get; set; }    
        public double Commission { get; set; } 
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }

        public double DurationSeconds => (ExitTime - EntryTime).TotalSeconds;
    }
}