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

    [Flags]
    public enum TickFlags : byte
    {
        None = 0,
        Bid = 1,    
        Ask = 2,    
        Trade = 4   
    }
}