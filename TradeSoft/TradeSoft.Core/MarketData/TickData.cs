using System.Runtime.InteropServices;

namespace TradeSoft.Core.MarketData;

/// <summary>
/// Contrat binaire : 33 bytes (Pack=1).
/// Timestamp = long (souvent DateTime.Ticks OU unix nanos, à standardiser dans tout le projet).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct TickData
{
    public readonly long Timestamp; // 8
    public readonly double Bid;     // 8
    public readonly double Ask;     // 8
    public readonly long Volume;     // 8
    public readonly TickFlags Flags;// 1

    public TickData(long timestamp, double bid, double ask, long volume, TickFlags flags = TickFlags.Quote)
    {
        Timestamp = timestamp;
        Bid = bid;
        Ask = ask;
        Volume = volume;
        Flags = flags;
    }

    public double Mid => (Bid + Ask) * 0.5;
    public double Spread => Ask - Bid;

    public bool IsQuote => (Flags & TickFlags.Quote) != 0;
    public bool IsTrade => (Flags & TickFlags.Trade) != 0;
    public bool IsSimulated => (Flags & TickFlags.Simulated) != 0;

    public override string ToString()
        => $"t={Timestamp} bid={Bid} ask={Ask} vol={Volume} flags={Flags}";
}
