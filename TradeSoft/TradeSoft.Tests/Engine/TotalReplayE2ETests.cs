using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed class RealisticReplayE2ETests
{
    [Fact]
    public async Task ReplayBinaryTicks_ShouldTriggerStrategy_Risk_Matching_Broker_EndToEnd()
    {
        // Arrange
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var file1 = Path.Combine(tempDir.FullName, "EURUSD_2019.bin");
            var file2 = Path.Combine(tempDir.FullName, "EURUSD_2020.bin");

            // TickData = 33 bytes : long ts, double bid, double ask, long vol, byte flags
            var ticks1 = new[]
            {
                new TickData(1, 1.1000, 1.1002, 10),
                new TickData(2, 1.1001, 1.1003, 20),
            };

            var triggerTick = new TickData(3, 1.2000, 1.2004, 99); // <- Ask = 1.2004
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
            var matching = new ImmediateMarketMatchingEngine();
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

            // Act
            await publisher.PublishAsync();

            // Assert
            done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue("le broker doit recevoir un ExecutionReport généré par matching");

            broker.Reports.Should().HaveCount(1);
            var report = broker.Reports[0];

            report.Status.Should().Be(ExecutionStatus.Filled);
            report.FilledQuantity.Should().Be(2.5m);
            report.AveragePrice.Should().Be(1.2004m); // BUY => exécuté au Ask du tick trigger

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

            var cmd = new OrderCommand(
                commandId: Guid.NewGuid(),
                symbol: _symbol,
                side: OrderSide.Buy,
                quantity: _qty,
                type: OrderType.Market,
                timeInForce: OrderTimeInForce.Day);

            e.AttachOrder(cmd);
        }
    }

    private sealed class AlwaysPassRiskManager : IRiskManager
    {
        public int ValidateCalls { get; private set; }

        public void Validate(MarketEvent e)
        {
            ValidateCalls++;
        }
    }

    private sealed class ImmediateMarketMatchingEngine : IMatchingEngine
    {
        public int ExecuteCalls { get; private set; }

        public ExecutionReport Execute(OrderCommand command, TickData? tick)
        {
            ExecuteCalls++;

            if (tick is null)
            {
                return new ExecutionReport(command.CommandId, ExecutionStatus.Rejected, 0m, null, "No tick context");
            }

            var px = command.Side == OrderSide.Buy ? (decimal)tick.Value.Ask : (decimal)tick.Value.Bid;

            return new ExecutionReport(
                commandId: command.CommandId,
                status: ExecutionStatus.Filled,
                filledQuantity: command.Quantity,
                averagePrice: px,
                message: "Immediate market fill");
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

        public void OnExecutionReport(MarketEvent e)
        {
            if (e.ExecutionReport is null) return;

            Reports.Add(e.ExecutionReport);

            if (Reports.Count >= _expected)
                _done.Set();
        }
    }
}
