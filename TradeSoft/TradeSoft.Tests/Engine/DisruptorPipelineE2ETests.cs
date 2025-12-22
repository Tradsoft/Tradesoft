using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using Xunit;

namespace TradeSoft.Tests.Engine;

public sealed class DisruptorPipelineE2ETests
{
    [Fact]
    public void Tick_To_Order_To_Risk_To_Matching_To_Broker_Should_Produce_Filled_Report()
    {
        // Arrange
        using var completion = new ManualResetEventSlim(false);

        var triggerTimestamp = 3L;
        var expectedSymbol = "EURUSD";
        var expectedQty = 2.5m;

        var broker = new CapturingBroker(expectedReports: 1, completion);
        var matching = new ImmediateFillMatchingEngine();
        var risk = new AlwaysPassRiskManager();
        var strategy = new TriggerOrderStrategy(triggerTimestamp, expectedSymbol, expectedQty);

        var runner = new SimulationRunner(
            DisruptorConfig.Default,
            strategy: strategy,
            riskManager: risk,
            matchingEngine: matching,
            broker: broker);

        runner.Start();

        try
        {
            // Publish a few ticks (timestamp 1..5). Trigger is at ts=3
            runner.PublishTick(new TickData(1, 1.1000, 1.1002, 10));
            runner.PublishTick(new TickData(2, 1.1001, 1.1003, 10));
            runner.PublishTick(new TickData(3, 1.2000, 1.2004, 99));
            runner.PublishTick(new TickData(4, 1.1002, 1.1005, 10));
            runner.PublishTick(new TickData(5, 1.1003, 1.1006, 10));

            // Assert wait for broker to receive a report
            completion.Wait(TimeSpan.FromSeconds(2))
                .Should().BeTrue("le broker doit recevoir un ExecutionReport après le matching");

            broker.Reports.Should().HaveCount(1);
            var report = broker.Reports[0];

            report.Status.Should().Be(ExecutionStatus.Filled);
            report.FilledQuantity.Should().Be(expectedQty);

            // BUY => prix d'exécution = Ask du tick trigger (1.2004)
            report.AveragePrice.Should().Be(1.2004m);

            // Le risk et le matching ont bien été traversés
            risk.ValidateCalls.Should().Be(1);
            matching.ExecuteCalls.Should().Be(1);
        }
        finally
        {
            runner.Stop();
        }
    }

    // -----------------------
    // Fakes / Test doubles
    // -----------------------

    private sealed class TriggerOrderStrategy : IStrategy
    {
        private readonly long _triggerTimestamp;
        private readonly string _symbol;
        private readonly decimal _qty;

        public TriggerOrderStrategy(long triggerTimestamp, string symbol, decimal qty)
        {
            _triggerTimestamp = triggerTimestamp;
            _symbol = symbol;
            _qty = qty;
        }

        public void OnMarketEvent(MarketEvent marketEvent)
        {
            if (marketEvent.Tick is null) return;

            if (marketEvent.Tick.Value.Timestamp == _triggerTimestamp)
            {
                var cmd = new OrderCommand(
                    commandId: Guid.NewGuid(),
                    symbol: _symbol,
                    side: OrderSide.Buy,
                    quantity: _qty,
                    type: OrderType.Market,
                    timeInForce: OrderTimeInForce.Day,
                    limitPrice: null);

                marketEvent.AttachOrder(cmd);
            }
        }
    }

    private sealed class AlwaysPassRiskManager : IRiskManager
    {
        public int ValidateCalls { get; private set; }

        public void Validate(MarketEvent marketEvent)
        {
            ValidateCalls++;
        }
    }

    private sealed class ImmediateFillMatchingEngine : IMatchingEngine
    {
        public int ExecuteCalls { get; private set; }

        public ExecutionReport Execute(OrderCommand command, TickData? tick)
        {
            ExecuteCalls++;

            if (tick is null)
            {
                return new ExecutionReport(
                    commandId: command.CommandId,
                    status: ExecutionStatus.Rejected,
                    filledQuantity: 0m,
                    averagePrice: null,
                    message: "No tick context.");
            }

            var px = command.Side == OrderSide.Buy
                ? (decimal)tick.Value.Ask
                : (decimal)tick.Value.Bid;

            return new ExecutionReport(
                commandId: command.CommandId,
                status: ExecutionStatus.Filled,
                filledQuantity: command.Quantity,
                averagePrice: px,
                message: "Immediate fill");
        }
    }

    private sealed class CapturingBroker : IBroker
    {
        private readonly int _expectedReports;
        private readonly ManualResetEventSlim _completion;

        public CapturingBroker(int expectedReports, ManualResetEventSlim completion)
        {
            _expectedReports = expectedReports;
            _completion = completion;
        }

        public List<ExecutionReport> Reports { get; } = new();

        public int TicksSeen { get; private set; }

        public void OnTick(in TickData tick)
        {
            TicksSeen++;
        }

        public void OnExecutionReport(ExecutionReport report)
        {
            Reports.Add(report);

            if (Reports.Count >= _expectedReports)
                _completion.Set();
        }
    }
}
