using System;
using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;
using Xunit;

namespace TradeSoft.Tests.Engine;

public class SimulationRunnerSmokeTests
{
    [Fact]
    public void PipelineStartsPublishesAndStops()
    {
        var broker = new NoOpBroker();

        var runner = new SimulationRunner(
            DisruptorConfig.Default,
            strategy: new NoOpStrategy(),
            riskManager: new NoOpRiskManager(),
            matchingEngine: new NoOPMatchingEngine(),
            broker: broker);

        runner.Start();

        runner.PublishTick(new TickData(1, 100, 101, 10));
        runner.PublishExecutionReport(new ExecutionReport(Guid.NewGuid(), ExecutionStatus.New, 0m));

        runner.Stop();

        runner.IsRunning.Should().BeFalse();
        broker.ExecutionReportsReceived.Should().Be(1);
    }


    private sealed class NoOpStrategy : IStrategy
    {
        public void OnMarketEvent(MarketEvent marketEvent) { }
    }

    private sealed class NoOpRiskManager : IRiskManager
    {
        public void Validate(MarketEvent marketEvent) { }
    }

    private sealed class NoOPMatchingEngine : IMatchingEngine
    {
        public ExecutionReport Execute(OrderCommand command, TickData? tick)
            => new ExecutionReport(command.CommandId, ExecutionStatus.Rejected, 0m, null, "NoOp");
    }

    private sealed class NoOpBroker : IBroker
    {
        public int ExecutionReportsReceived { get; private set; }
        public int TicksSeen { get; private set; }

        public void OnTick(in TickData tick) => TicksSeen++;

        public void OnExecutionReport(ExecutionReport report)
        {
            ExecutionReportsReceived++;
        }
    }

}
