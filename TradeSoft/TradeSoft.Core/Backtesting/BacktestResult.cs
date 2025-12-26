using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

/// <summary>
/// Résultat d'un backtest : equity curve + méta + stats simples.
/// </summary>
public sealed class BacktestResult
{
    public BacktestResult(
        string strategyName,
        string symbol,
        IReadOnlyList<EquitySnapshot> equityCurve)
    {
        StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
        Symbol = string.IsNullOrWhiteSpace(symbol) ? throw new ArgumentException("Symbol is required.", nameof(symbol)) : symbol;
        EquityCurve = equityCurve ?? throw new ArgumentNullException(nameof(equityCurve));
    }

    public string StrategyName { get; }
    public string Symbol { get; }
    public IReadOnlyList<EquitySnapshot> EquityCurve { get; }

    public bool HasCurve => EquityCurve.Count > 0;

    public long? StartTimestamp => HasCurve ? EquityCurve[0].Timestamp : null;
    public long? EndTimestamp => HasCurve ? EquityCurve[^1].Timestamp : null;

    public decimal? StartEquity => HasCurve ? EquityCurve[0].NetLiquidationValue : null;
    public decimal? EndEquity => HasCurve ? EquityCurve[^1].NetLiquidationValue : null;

    public decimal? TotalPnl
        => (StartEquity is null || EndEquity is null) ? null : (EndEquity.Value - StartEquity.Value);

    /// <summary>Retour total simple. Null si StartEquity <= 0.</summary>
    public decimal? TotalReturn
    {
        get
        {
            if (StartEquity is null || EndEquity is null) return null;
            var start = StartEquity.Value;
            if (start <= 0m) return null;
            return (EndEquity.Value / start) - 1m;
        }
    }

    /// <summary>
    /// Max Drawdown sur NLV (peak-to-trough), valeur négative (ex: -0.12 = -12%).
    /// Null si courbe vide ou equity <= 0 à un moment.
    /// </summary>
    public decimal? MaxDrawdown
    {
        get
        {
            if (!HasCurve) return null;

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
}
