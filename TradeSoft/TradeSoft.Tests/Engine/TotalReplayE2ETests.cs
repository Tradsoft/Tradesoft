using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using TradeSoft.Infrastructure.Streaming;
using Xunit;

namespace TradeSoft.Tests.Engine;

public sealed class TotalReplayE2ETests
{
    [Fact]
    public async Task ReplayBinaryTicks_ShouldTriggerStrategy_Risk_Matching_Broker_EndToEnd()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var file1 = Path.Combine(tempDir.FullName, "EURUSD_2019.bin");
            var file2 = Path.Combine(tempDir.FullName, "EURUSD_2020.bin");

            var ticks1 = new[]
            {
                new TickData(1, 1.1000, 1.1002, 10),
                new TickData(2, 1.1001, 1.1003, 20),
            };

            var triggerTick = new TickData(3, 1.2000, 1.2004, 99);
            var ticks2 = new[]
            {
                triggerTick,
                new TickData(4, 1.1002, 1.1005, 30),
                new TickData(5, 1.1003, 1.1006, 40),
            };

            WriteTicks(file1, ticks1);
            WriteTicks(file2, ticks2);

            using var done = new ManualResetEventSlim(false);

            var broker = new CapturingBroker(expectedReports: 1, done);
            var matching = new ImmediateFillMatchingEngine();
            var risk = new AlwaysPassRiskManager();
            var strategy = new TriggerBuyStrategy(triggerTimestamp: 3, symbol: "EURUSD", qty: 2.5m);

            var runner = new SimulationRunner(
                DisruptorConfig.Default,
                strategy: strategy,
                riskManager: risk,
                matchingEngine: matching,
                broker: broker);

            runner.Start();

            var publisher = new MarketDataPublisher(
                runner,
                new MultiFileStreamer(new[] { file1, file2 }),
                new BinaryTickReader());

            await publisher.PublishAsync();

            done.Wait(TimeSpan.FromSeconds(2))
                .Should().BeTrue("le broker doit recevoir un ExecutionReport généré par matching");

            broker.Reports.Should().HaveCount(1);
            var report = broker.Reports[0];

            report.Status.Should().Be(ExecutionStatus.Filled);
            report.FilledQuantity.Should().Be(2.5m);
            report.AveragePrice.Should().Be(1.2004m);

            risk.ValidateCalls.Should().Be(1);
            matching.ExecuteCalls.Should().Be(1);

            runner.Stop();
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    private static void WriteTicks(string path, IEnumerable<TickData> ticks)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        foreach (var t in ticks)
        {
            writer.Write(t.Timestamp);
            writer.Write(t.Bid);
            writer.Write(t.Ask);
            writer.Write(t.Volume);
            writer.Write((byte)t.Flags);
        }
    }

    // ----------------- Test doubles -----------------

    private sealed class TriggerBuyStrategy : IStrategy
    {
        private readonly long _triggerTimestamp;
        private readonly string _symbol;
        private readonly decimal _qty;

        public TriggerBuyStrategy(long triggerTimestamp, string symbol, decimal qty)
        {
            _triggerTimestamp = triggerTimestamp;
            _symbol = symbol;
            _qty = qty;
        }

        public void OnMarketEvent(MarketEvent e)
        {
            if (e.Tick is null) return;
            if (e.Tick.Value.Timestamp != _triggerTimestamp) return;

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
        public int ValidateCalls { get; private set; }
        public void Validate(MarketEvent e) => ValidateCalls++;
    }

    private sealed class ImmediateFillMatchingEngine : IMatchingEngine
    {
        public int ExecuteCalls { get; private set; }

        public void Process(MarketEvent e)
        {
            if (e.Order is null) return;
            if (e.IsRejected) return;
            if (e.ReportCount > 0) return;     // v4+
            if (e.Tick is null) return;

            ExecuteCalls++;

            var tick = e.Tick.Value;
            var cmd = e.Order;

            var basePx = cmd.Side == OrderSide.Buy ? (decimal)tick.Ask : (decimal)tick.Bid;

            e.AttachExecutionReport(new ExecutionReport(
                commandId: cmd.CommandId,
                symbol: cmd.Symbol,
                side: cmd.Side,
                status: ExecutionStatus.Filled,
                filledQuantity: cmd.Quantity,
                averagePrice: basePx,
                message: "Immediate fill"));
        }
    }

    private sealed class CapturingBroker : IBroker
    {
        private readonly int _expected;
        private readonly ManualResetEventSlim _done;

        public CapturingBroker(int expectedReports, ManualResetEventSlim done)
        {
            _expected = expectedReports;
            _done = done;
        }

        public List<ExecutionReport> Reports { get; } = new();
        public int TicksSeen { get; private set; }

        public void OnTick(in TickData tick) => TicksSeen++;

        public void OnExecutionReport(ExecutionReport report)
        {
            Reports.Add(report);
            if (Reports.Count >= _expected) _done.Set();
        }
    }
}
