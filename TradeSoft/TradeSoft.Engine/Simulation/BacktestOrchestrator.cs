using System;
using System.Linq;
using System.Threading.Tasks;
using TradeSoft.Core.Backtesting;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;

namespace TradeSoft.Engine.Backtesting;

public sealed class BacktestOrchestrator
{
    public async Task<BacktestRunResult> RunAsync(
        string strategyName,
        string symbol,
        InstrumentSpec spec,
        decimal initialCash,
        DisruptorConfig disruptorConfig,
        IStrategy strategy,
        IRiskManager riskManager,
        IMatchingEngine matchingEngine,
        Func<Task> publishAsync)
    {
        if (string.IsNullOrWhiteSpace(strategyName)) throw new ArgumentException("StrategyName is required.", nameof(strategyName));
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (spec is null) throw new ArgumentNullException(nameof(spec));
        if (strategy is null) throw new ArgumentNullException(nameof(strategy));
        if (riskManager is null) throw new ArgumentNullException(nameof(riskManager));
        if (matchingEngine is null) throw new ArgumentNullException(nameof(matchingEngine));
        if (publishAsync is null) throw new ArgumentNullException(nameof(publishAsync));

        var simBroker = new SimulatedBroker(spec, initialCash: initialCash);
        var recorder = new EquityCurveRecorder(simBroker /* IBrokerState */, symbol);
        var ledger = new TradeLedger();
        var composite = new CompositeBroker(simBroker, ledger, recorder);

        var runner = new SimulationRunner(
            disruptorConfig,
            strategy: strategy,
            riskManager: riskManager,
            broker: composite,
            matchingEngine: matchingEngine);

        runner.Start();

        try
        {
            await publishAsync();

            var curve = recorder.Curve.ToArray();
            var fills = ledger.Fills.ToArray();

            var result = new BacktestResult(strategyName, symbol, curve);
            return new BacktestRunResult(result, fills);
        }
        finally
        {
            runner.Stop();
        }
    }
}

/// <summary>
/// Ce que tu sors à la fin du backtest (mid-frequency) : curve + fills.
/// </summary>
public readonly record struct BacktestRunResult(
    BacktestResult Result,
    TradeFill[] Fills
);
