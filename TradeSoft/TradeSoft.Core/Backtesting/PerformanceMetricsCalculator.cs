using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

public static class PerformanceMetricsCalculator
{
    /// <summary>
    /// Calcule les metrics à partir de la courbe (MDD / total return) et des returns (vol/sharpe/hit/etc).
    /// annualizationFactor : si tu veux annualiser Sharpe => Sharpe_bucket * sqrt(factor).
    /// Pour v1 mid-frequency : laisse null.
    /// </summary>
    public static PerformanceMetrics Compute(
        IReadOnlyList<EquitySnapshot> equityCurve,
        IReadOnlyList<BucketReturn> returns,
        decimal riskFreeRatePerPeriod = 0m,
        double? annualizationFactor = null)
    {
        if (equityCurve is null) throw new ArgumentNullException(nameof(equityCurve));
        if (returns is null) throw new ArgumentNullException(nameof(returns));

        var totalReturn = ComputeTotalReturn(equityCurve);
        var maxDrawdown = ComputeMaxDrawdown(equityCurve);

        var periods = returns.Count;
        if (periods == 0)
        {
            return new PerformanceMetrics
            {
                Periods = 0,
                TotalReturn = totalReturn,
                MaxDrawdown = maxDrawdown,
                Volatility = 0m,
                Sharpe = null,
                HitRatio = 0m,
                AvgReturn = 0m,
                MedianReturn = 0m
            };
        }

        var avg = AverageReturn(returns);
        var median = MedianReturn(returns);
        var hit = HitRatio(returns);

        var vol = StdDevSample(returns);

        decimal? sharpe = null;
        if (vol > 0m)
        {
            var excessMean = avg - riskFreeRatePerPeriod;
            var baseSharpe = excessMean / vol;

            if (annualizationFactor is double af && af > 0.0)
            {
                var scale = (decimal)Math.Sqrt(af);
                sharpe = baseSharpe * scale;
            }
            else
            {
                sharpe = baseSharpe;
            }
        }

        return new PerformanceMetrics
        {
            Periods = periods,
            TotalReturn = totalReturn,
            MaxDrawdown = maxDrawdown,
            Volatility = vol,
            Sharpe = sharpe,
            HitRatio = hit,
            AvgReturn = avg,
            MedianReturn = median
        };
    }

    private static decimal? ComputeTotalReturn(IReadOnlyList<EquitySnapshot> curve)
    {
        if (curve.Count < 2) return null;

        var start = curve[0].NetLiquidationValue;
        var end = curve[^1].NetLiquidationValue;

        if (start <= 0m) return null;
        return (end / start) - 1m;
    }

    /// <summary>Max drawdown sur NLV : min(NLV/peak - 1).</summary>
    private static decimal ComputeMaxDrawdown(IReadOnlyList<EquitySnapshot> curve)
    {
        if (curve.Count == 0) return 0m;

        var peak = curve[0].NetLiquidationValue;
        var mdd = 0m;

        for (int i = 0; i < curve.Count; i++)
        {
            var x = curve[i].NetLiquidationValue;

            if (x > peak) peak = x;

            if (peak > 0m)
            {
                var dd = (x / peak) - 1m;
                if (dd < mdd) mdd = dd;
            }
        }

        return mdd;
    }

    private static decimal AverageReturn(IReadOnlyList<BucketReturn> returns)
    {
        decimal sum = 0m;
        for (int i = 0; i < returns.Count; i++) sum += returns[i].Return;
        return sum / returns.Count;
    }

    private static decimal MedianReturn(IReadOnlyList<BucketReturn> returns)
    {
        var arr = new decimal[returns.Count];
        for (int i = 0; i < returns.Count; i++) arr[i] = returns[i].Return;

        Array.Sort(arr);

        int n = arr.Length;
        if (n % 2 == 1) return arr[n / 2];
        return (arr[(n / 2) - 1] + arr[n / 2]) / 2m;
    }

    private static decimal HitRatio(IReadOnlyList<BucketReturn> returns)
    {
        int wins = 0;
        for (int i = 0; i < returns.Count; i++)
            if (returns[i].Return > 0m) wins++;

        return (decimal)wins / returns.Count;
    }

    /// <summary>StdDev sample (n-1). Retourne 0 si n&lt;2.</summary>
    private static decimal StdDevSample(IReadOnlyList<BucketReturn> returns)
    {
        int n = returns.Count;
        if (n < 2) return 0m;

        double mean = 0.0;
        double m2 = 0.0;
        int k = 0;

        for (int i = 0; i < n; i++)
        {
            k++;
            double x = (double)returns[i].Return;
            double delta = x - mean;
            mean += delta / k;
            double delta2 = x - mean;
            m2 += delta * delta2;
        }

        double variance = m2 / (n - 1);
        if (variance <= 0.0) return 0m;

        return (decimal)Math.Sqrt(variance);
    }
}
