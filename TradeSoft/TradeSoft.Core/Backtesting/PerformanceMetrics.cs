namespace TradeSoft.Core.Backtesting;

/// <summary>
/// Metrics de perf calculées sur une série de returns (bucketisée) + equity curve.
/// </summary>
public sealed class PerformanceMetrics
{
    public int Periods { get; init; }                      
    public decimal? TotalReturn { get; init; }             
    public decimal MaxDrawdown { get; init; }              
    public decimal Volatility { get; init; }               
    public decimal? Sharpe { get; init; }                  
    public decimal HitRatio { get; init; }                 
    public decimal AvgReturn { get; init; }
    public decimal MedianReturn { get; init; }

    public override string ToString()
        => $"Periods={Periods}, TotalReturn={TotalReturn}, MDD={MaxDrawdown}, Vol={Volatility}, Sharpe={Sharpe}, Hit={HitRatio}";
}
