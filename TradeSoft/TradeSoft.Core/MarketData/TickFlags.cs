namespace TradeSoft.Core.MarketData;

[System.Flags]
public enum TickFlags : byte
{
    None = 0,
    Quote = 1 << 0, // Bid/Ask update
    Trade = 1 << 1, // Last trade event (si tu l’as)
    Simulated = 1 << 2, // Data générée / altérée
}
