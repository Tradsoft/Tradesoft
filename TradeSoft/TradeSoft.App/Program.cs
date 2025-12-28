using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradeSoft.Core.Backtesting;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Backtesting;
using TradeSoft.Engine.Simulation;
using TradeSoft.Engine.Simulation.Models;

internal static class Program
{
    public static async Task Main()
    {
        var spec = new InstrumentSpec("EURUSD", priceScalePow10: 5);

        IStrategy strategy = new DebugBuyOnceStrategy("EURUSD", 50m);
        IRiskManager risk = new AlwaysPassRiskManager();
        ILatencyModel latency = new FixedLatencyModel(0);
        ISlippageModel slippage = new VolumeShareSlippageModel(maxParticipation: 0.2m, impactPipsPer1x: 2.0m);
        IMatchingEngine matching = new MatchingEngine(spec, latency, slippage);

        var orchestrator = new BacktestOrchestrator();

        Func<SimulationRunner, Task> publishAsync = runner =>
        {
            var ticks = BuildFakeTicks();
            foreach (var t in ticks)
                runner.PublishTick(t);

            return Task.CompletedTask;
        };

        var bucket = new BucketSpec(Size: 10, Mode: BucketingMode.Last);

        var run = await orchestrator.RunAsync(
            strategyName: "DebugStrat",
            symbol: "EURUSD",
            spec: spec,
            initialCash: 100_000m,
            disruptorConfig: DisruptorConfig.Default,
            strategy: strategy,
            riskManager: risk,
            matchingEngine: matching,
            bucket: bucket,
            publishAsync: publishAsync);

        Console.WriteLine($"Curve points: {run.Result.EquityCurve.Count}");
        Console.WriteLine($"Fills: {run.Fills.Length}");
        Console.WriteLine($"TotalReturn: {run.Result.TotalReturn}");
        Console.WriteLine($"MaxDrawdown: {run.Result.MaxDrawdown}");
        Console.WriteLine($"Metrics.Vol: {run.Result.Metrics?.Volatility}");
        Console.WriteLine($"Metrics.Sharpe: {run.Result.Metrics?.Sharpe}");
    }

    private static List<TickData> BuildFakeTicks()
    {
        return new List<TickData>
        {
            new TickData(0,   1.2000, 1.2004, volume: 10),
            new TickData(1,   1.2000, 1.2004, volume: 10),
            new TickData(9,   1.2000, 1.2004, volume: 10),
            new TickData(10,  1.2000, 1.2004, volume: 10),
            new TickData(11,  1.2000, 1.2004, volume: 10),
            new TickData(19,  1.2000, 1.2004, volume: 10),
            new TickData(29,  1.2000, 1.2004, volume: 10),
        };
    }

    private sealed class DebugBuyOnceStrategy : IStrategy
    {
        private readonly string _symbol;
        private readonly decimal _qty;
        private bool _sent;

        public DebugBuyOnceStrategy(string symbol, decimal qty)
        {
            _symbol = symbol;
            _qty = qty;
        }

        public void OnMarketEvent(TradeSoft.Core.Events.MarketEvent e)
        {
            if (_sent) return;
            if (e.Tick is null) return;

            if (e.Tick.Value.Timestamp < 1) return;

            _sent = true;
            e.AttachOrder(new OrderCommand(
                commandId: Guid.NewGuid(),
                symbol: _symbol,
                side: OrderSide.Buy,
                quantity: _qty,
                type: OrderType.Market,
                timeInForce: OrderTimeInForce.Day));
        }
    }

    private sealed class AlwaysPassRiskManager : IRiskManager
    {
        public void Validate(TradeSoft.Core.Events.MarketEvent marketEvent) { }
    }
}
