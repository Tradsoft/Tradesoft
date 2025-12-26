using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

public sealed class BacktestResultBuilder
{
    private readonly List<EquitySnapshot> _curve = new();

    public BacktestResultBuilder(string strategyName, string symbol)
    {
        StrategyName = strategyName ?? throw new ArgumentNullException(nameof(strategyName));
        Symbol = string.IsNullOrWhiteSpace(symbol) ? throw new ArgumentException("Symbol is required.", nameof(symbol)) : symbol;
    }

    public string StrategyName { get; }
    public string Symbol { get; }

    public IReadOnlyList<EquitySnapshot> EquityCurve => _curve;

    public void Add(in EquitySnapshot snapshot) => _curve.Add(snapshot);

    public BacktestResult Build()
        => new BacktestResult(StrategyName, Symbol, _curve.ToArray());
}
