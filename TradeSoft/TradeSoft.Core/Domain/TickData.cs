using System.Runtime.InteropServices;

namespace TradeSoft.Core.Domain
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct TickData
    {
        public readonly long Timestamp; // 8 octets 
        public readonly double Bid;     // 8 octets 
        public readonly double Ask;     // 8 octets 
        public readonly int Volume;     // 4 octets 
        public readonly TickFlags Flags;// 1 octet

        public bool IsTrade => (Flags & TickFlags.Trade) != 0;
        public double MidPrice => (Bid + Ask) / 2.0;
    }
}