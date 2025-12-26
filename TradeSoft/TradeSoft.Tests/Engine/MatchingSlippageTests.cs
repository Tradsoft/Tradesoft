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

public sealed class MatchingSlippageTests
{
    [Fact]
    public void BigBuy_OnLowVolume_ShouldSlipAboveAsk()
    {
        using var done = new ManualResetEventSlim(false);

        var spec = new InstrumentSpec("EURUSD", 5);
        var latency = new FixedLatencyModel(0);
        var slippage = new VolumeShareSlippageModel(maxParticipation: 0.2m, impactPipsPer1x: 2.0m);
        var matching = new MatchingEngine(spec, latency, slippage);

        var broker = new CapturingBroker(expected: 1, done);
        var risk = new AlwaysPassRiskManager();
        var strategy = new BuyNow("EURUSD", qty: 50m);

        var runner = new SimulationRunner(
            DisruptorConfig.Default,
            strategy: strategy,
            riskManager: risk,
            matchingEngine: matching,
            broker: broker);

        runner.Start();
        try
        {
            runner.PublishTick(new TickData(1, 1.2000, 1.2004, volume: 10));

            var t2 = new TickData(2, 1.2000, 1.2004, volume: 10);
            runner.PublishTick(t2);

            done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();

            broker.Reports.Should().NotBeEmpty();
            var report = broker.Reports[0];

            report.Status.Should().Be(ExecutionStatus.PartiallyFilled);
            report.FilledQuantity.Should().Be(2.0m);
            report.AveragePrice.Should().NotBeNull();
            report.AveragePrice!.Value.Should().BeGreaterThan((decimal)t2.Ask);
        }
        finally
        {
            runner.Stop();
        }
    }


    private sealed class BuyNow : IStrategy
    {
        private readonly string _symbol;
        private readonly decimal _qty;
        private bool _sent;

        public BuyNow(string symbol, decimal qty) { _symbol = symbol; _qty = qty; }

        public void OnMarketEvent(MarketEvent e)
        {
            if (_sent) return;
            if (e.Tick is null) return;

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
