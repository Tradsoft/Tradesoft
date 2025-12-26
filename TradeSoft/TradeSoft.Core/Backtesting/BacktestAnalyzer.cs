using System;
using System.Collections.Generic;
using System.Linq;

namespace TradeSoft.Core.Backtesting;

public sealed class BacktestAnalyzer
{
    public BacktestAnalyzer(BucketSpec bucket)
    {
        Bucket = bucket.IsValid ? bucket : throw new ArgumentOutOfRangeException(nameof(bucket));
    }

    public BucketSpec Bucket { get; }

    public BacktestAnalysis Analyze(BacktestResult result)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        if (result.EquityCurve.Count == 0)
            return new BacktestAnalysis(result.StrategyName, result.Symbol,
                bucketedCurve: Array.Empty<EquitySnapshot>(),
                returns: Array.Empty<BucketReturn>());

        var bucketed = EquityBucketer.Bucketize(result.EquityCurve, Bucket);
        var returns = ReturnSeries.ComputeSimpleReturns(bucketed);

        return new BacktestAnalysis(result.StrategyName, result.Symbol, bucketed, returns);
    }
}

public sealed class BacktestAnalysis
{
    public BacktestAnalysis(
        string strategyName,
        string symbol,
        IReadOnlyList<EquitySnapshot> bucketedCurve,
        IReadOnlyList<BucketReturn> returns)
    {
        StrategyName = strategyName;
        Symbol = symbol;
        BucketedCurve = bucketedCurve;
        Returns = returns;
    }

    public string StrategyName { get; }
    public string Symbol { get; }

    public IReadOnlyList<EquitySnapshot> BucketedCurve { get; }
    public IReadOnlyList<BucketReturn> Returns { get; }

    public decimal? TotalReturn
    {
        get
        {
            if (BucketedCurve.Count < 2) return null;
            var start = BucketedCurve[0].NetLiquidationValue;
            var end = BucketedCurve[^1].NetLiquidationValue;
            if (start <= 0m) return null;
            return (end / start) - 1m;
        }
    }
}
