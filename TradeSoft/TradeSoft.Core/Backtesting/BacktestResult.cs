using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

/// <summary>
/// Résultat d'un backtest : equity curve brute + (optionnel) analytics bucketisés (returns + metrics).
/// Mid-frequency v4+ : tu bucketizes puis tu calcules les stats sur les returns.
/// </summary>
public sealed class BacktestResult
{
    // Backward-compatible ctor
    public BacktestResult(
        string strategyName,
        string symbol,
        IReadOnlyList<EquitySnapshot> equityCurve)
        : this(strategyName, symbol, equityCurve,
              bucketSpec: null,
              bucketedEquityCurve: null,
              returns: null,
              metrics: null)
    { }
    public BacktestResult(
        string strategyName,
        string symbol,
        IReadOnlyList<EquitySnapshot> equityCurve,
        BucketSpec? bucketSpec,
        IReadOnlyList<EquitySnapshot>? bucketedEquityCurve,
        IReadOnlyList<BucketReturn>? returns,
        PerformanceMetrics? metrics)
    {
        StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
        Symbol = string.IsNullOrWhiteSpace(symbol) ? throw new ArgumentException("Symbol is required.", nameof(symbol)) : symbol;
        EquityCurve = equityCurve ?? throw new ArgumentNullException(nameof(equityCurve));

        BucketSpec = bucketSpec;
        BucketedEquityCurve = bucketedEquityCurve;
        Returns = returns;
        Metrics = metrics;
    }

    public string StrategyName { get; }
    public string Symbol { get; }

    /// <summary>Courbe brute (souvent snapshot à chaque tick).</summary>
    public IReadOnlyList<EquitySnapshot> EquityCurve { get; }
    public BucketSpec? BucketSpec { get; }
    public IReadOnlyList<EquitySnapshot>? BucketedEquityCurve { get; }
    public IReadOnlyList<BucketReturn>? Returns { get; }
    public PerformanceMetrics? Metrics { get; }

    public bool HasCurve => EquityCurve.Count > 0;
    public long? StartTimestamp => HasCurve ? EquityCurve[0].Timestamp : null;
    public long? EndTimestamp => HasCurve ? EquityCurve[^1].Timestamp : null;
    public decimal? StartEquity => HasCurve ? EquityCurve[0].NetLiquidationValue : null;
    public decimal? EndEquity => HasCurve ? EquityCurve[^1].NetLiquidationValue : null;
    public decimal? TotalPnl
        => (StartEquity is null || EndEquity is null) ? null : (EndEquity.Value - StartEquity.Value);

    /// <summary>Total return simple; si Metrics est dispo, on renvoie Metrics.TotalReturn.</summary>
    public decimal? TotalReturn
    {
        get
        {
            if (Metrics?.TotalReturn is decimal tr) return tr;

            if (StartEquity is null || EndEquity is null) return null;
            var start = StartEquity.Value;
            if (start <= 0m) return null;
            return (EndEquity.Value / start) - 1m;
        }
    }

    /// <summary>Max drawdown; si Metrics dispo, renvoie Metrics.MaxDrawdown.</summary>
    public decimal? MaxDrawdown
    {
        get
        {
            if (!HasCurve) return null;
            if (Metrics is not null) return Metrics.MaxDrawdown;

            decimal peak = EquityCurve[0].NetLiquidationValue;
            if (peak <= 0m) return null;

            decimal maxDd = 0m;
            for (int i = 1; i < EquityCurve.Count; i++)
            {
                var x = EquityCurve[i].NetLiquidationValue;
                if (x <= 0m) return null;

                if (x > peak) peak = x;

                var dd = (x / peak) - 1m;
                if (dd < maxDd) maxDd = dd;
            }

            return maxDd;
        }
    }

    /// <summary>
    /// Calcule BucketedCurve + Returns + Metrics et renvoie un nouveau BacktestResult enrichi.
    /// </summary>
    public BacktestResult WithAnalytics(
        BucketSpec bucket,
        decimal riskFreeRatePerPeriod = 0m,
        double? annualizationFactor = null)
    {
        var bucketed = EquityBucketer.Bucketize(EquityCurve, bucket);
        var rets = ReturnSeries.ComputeSimpleReturns(bucketed);
        var metrics = PerformanceMetricsCalculator.Compute(
            equityCurve: EquityCurve,
            returns: rets,
            riskFreeRatePerPeriod: riskFreeRatePerPeriod,
            annualizationFactor: annualizationFactor);

        return new BacktestResult(
            strategyName: StrategyName,
            symbol: Symbol,
            equityCurve: EquityCurve,
            bucketSpec: bucket,
            bucketedEquityCurve: bucketed,
            returns: rets,
            metrics: metrics);
    }
}
