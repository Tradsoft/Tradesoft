using System;
using System.Linq;
using System.Threading;
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
        BucketSpec? bucket,
        Func<SimulationRunner, Task> publishAsync,
        decimal riskFreeRatePerPeriod = 0m,
        double? annualizationFactor = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyName))
            throw new ArgumentException("StrategyName is required.", nameof(strategyName));
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (spec is null) throw new ArgumentNullException(nameof(spec));
        if (strategy is null) throw new ArgumentNullException(nameof(strategy));
        if (riskManager is null) throw new ArgumentNullException(nameof(riskManager));
        if (matchingEngine is null) throw new ArgumentNullException(nameof(matchingEngine));
        if (publishAsync is null) throw new ArgumentNullException(nameof(publishAsync));

        var simBroker = new SimulatedBroker(spec, initialCash: initialCash, fees: BrokerFees.None);
        var recorder = new EquityCurveRecorder(state: simBroker, symbol: symbol);
        var ledger = new TradeLedgerRecorder();
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
            ct.ThrowIfCancellationRequested();

            await publishAsync(runner).ConfigureAwait(false);

            var curve = recorder.Curve.ToArray();
            var fills = ledger.Fills.ToArray();

            var baseResult = new BacktestResult(strategyName, symbol, curve);

            var result = bucket is null
                ? baseResult
                : baseResult.WithAnalytics(
                    bucket: bucket.Value,
                    riskFreeRatePerPeriod: riskFreeRatePerPeriod,
                    annualizationFactor: annualizationFactor);

            return new BacktestRunResult(result, fills);
        }
        finally
        {
            runner.Stop();
        }
    }
}

public readonly record struct BacktestRunResult(
    BacktestResult Result,
    TradeFill[] Fills
);
