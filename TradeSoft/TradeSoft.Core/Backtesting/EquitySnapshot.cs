namespace TradeSoft.Core.Backtesting;

/// <summary>
/// Snapshot d'état portefeuille / equity à un timestamp donné (souvent un tick).
/// - Timestamp = TickData.Timestamp (long) pour rester cohérent avec ton moteur.
/// - MarkPrice = prix de liquidation utilisé pour le MTM (long => Bid, short => Ask).
/// </summary>
public readonly record struct EquitySnapshot(
    long Timestamp,
    string Symbol,
    decimal Bid,
    decimal Ask,
    decimal MarkPrice,
    decimal Cash,
    decimal Position,
    decimal AveragePrice,
    decimal NetLiquidationValue,
    decimal RealizedPnl,
    decimal UnrealizedPnl
)
{
    public bool HasPosition => Position != 0m;

    /// <summary>
    /// Long => liquidation at Bid, Short => liquidation at Ask.
    /// </summary>
    public string MarkSide => Position switch
    {
        > 0m => "Bid",
        < 0m => "Ask",
        _ => "None"
    };
}
