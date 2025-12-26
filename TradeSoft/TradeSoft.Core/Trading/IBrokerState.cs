namespace TradeSoft.Core.Backtesting;

public interface IBrokerState
{
    decimal Cash { get; }
    decimal Position { get; }
    decimal AveragePrice { get; }
    decimal RealizedPnl { get; }
    decimal UnrealizedPnl { get; }
    decimal NetLiquidationValue { get; }
    decimal? Bid { get; }
    decimal? Ask { get; }
    decimal? MarkPrice { get; }
}
