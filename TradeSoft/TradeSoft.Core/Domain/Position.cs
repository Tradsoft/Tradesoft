using System;

namespace TradeSoft.Core.Domain
{
    public class Position
    {
        public long Id { get; set; }  
        public string Symbol { get; set; } = string.Empty;
        public Side Side { get; set; }      
        public double Quantity { get; set; }
        public double EntryPrice { get; set; }
        public DateTime EntryTime { get; set; }
        public double CurrentPrice { get; set; }
        public double UnrealizedPnL
        {
            get
            {
                if (Side == Side.Buy)
                    return (CurrentPrice - EntryPrice) * Quantity;
                else
                    return (EntryPrice - CurrentPrice) * Quantity;
            }
        }
    }
}