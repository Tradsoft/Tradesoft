using System;
using System.Threading;
using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using TradeSoft.Engine.Simulation.Models;
using Xunit;

namespace TradeSoft.Tests.Engine;

public sealed class MatchingLatencyE2ETests
{
    [Fact]
    public void Order_SubmittedAtTs3_WithLatency2_ShouldFillOnlyAtTs5()
    {
        using var done = new ManualResetEventSlim(false);

        var spec = new InstrumentSpec("EURUSD", priceScalePow10: 5);
        var latency = new FixedLatencyModel(latencyTicks: 2);
        var slippage = new VolumeShareSlippageModel(maxParticipation: 1.0m, impactPipsPer1x: 0m);
        var matching = new MatchingEngine(spec, latency, slippage);

        var broker = new CapturingBroker(1, done);
        var risk = new AlwaysPassRiskManager();
        var strategy = new TriggerMarketBuyAtTs3("EURUSD", 2.5m);

        var runner = new SimulationRunner(
            DisruptorConfig.Default,
            strategy: strategy,
            riskManager: risk,
            matchingEngine: matching,
            broker: broker);

        runner.Start();
        try
        {
            runner.PublishTick(new TickData(1, 1.1000, 1.1002, 10));
            runner.PublishTick(new TickData(2, 1.1001, 1.1003, 10));

            // ts=3 : stratégie attache ordre, mais due=5 donc pas de fill ici
            runner.PublishTick(new TickData(3, 1.2000, 1.2004, 99));
            done.Wait(TimeSpan.FromMilliseconds(200)).Should().BeFalse("no fill at ts=3 (latency=2)");

            // ts=4 : toujours pas
            runner.PublishTick(new TickData(4, 1.1002, 1.1005, 99));
            done.Wait(TimeSpan.FromMilliseconds(200)).Should().BeFalse("no fill at ts=4 (latency=2)");

            // ts=5 : fill attendu
            var t5 = new TickData(5, 1.3000, 1.3004, 99);
            runner.PublishTick(t5);

            done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue("fill attendu à ts=5 (due=5)");

            broker.Reports.Should().HaveCount(1);
            broker.Reports[0].Status.Should().Be(ExecutionStatus.Filled);
            broker.Reports[0].AveragePrice.Should().Be((decimal)t5.Ask); // BUY => Ask
        }
        finally
        {
            runner.Stop();
        }
    }

    private sealed class TriggerMarketBuyAtTs3 : IStrategy
    {
        private readonly string _symbol;
        private readonly decimal _qty;

        public TriggerMarketBuyAtTs3(string symbol, decimal qty) { _symbol = symbol; _qty = qty; }

        public void OnMarketEvent(MarketEvent e)
        {
            if (e.Tick is null) return;
            if (e.Tick.Value.Timestamp != 3) return;

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
        public void Validate(MarketEvent marketEvent) { }
    }

    private sealed class CapturingBroker : IBroker
    {
        private readonly int _expected;
        private readonly ManualResetEventSlim _done;

        public CapturingBroker(int expected, ManualResetEventSlim done) { _expected = expected; _done = done; }
        public System.Collections.Generic.List<ExecutionReport> Reports { get; } = new();

        public void OnTick(in TickData tick) { }

        public void OnExecutionReport(ExecutionReport report)
        {
            Reports.Add(report);
            if (Reports.Count >= _expected) _done.Set();
        }
    }
}
