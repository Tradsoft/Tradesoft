using FluentAssertions;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation;

namespace TradeSoft.Tests.Engine;

public class SimulationRunnerSmokeTests
{
    [Fact]
    public void PipelineStartsPublishesAndStops()
    {
        var runner = new SimulationRunner(
            DisruptorConfig.Default,
            new NoOpStrategy(),
            new NoOpRiskManager(),
            new NoOpBroker());

        runner.Start();

        runner.PublishTick(new TickData(1, 100, 101, 10));
        runner.PublishOrder(new OrderCommand(Guid.NewGuid(), "TEST", OrderSide.Buy, 1m, OrderType.Market, OrderTimeInForce.Day));
        runner.PublishExecutionReport(new ExecutionReport(Guid.NewGuid(), ExecutionStatus.New, 0m));

        runner.Stop();

        runner.IsRunning.Should().BeFalse();
    }

    private sealed class NoOpStrategy : IStrategy
    {
        public void OnMarketEvent(MarketEvent marketEvent)
        {
        }
    }

    private sealed class NoOpRiskManager : IRiskManager
    {
        public void Validate(MarketEvent marketEvent)
        {
        }
    }

    private sealed class NoOpBroker : IBroker
    {
        public void OnOrder(MarketEvent marketEvent)
        {
        }
    }
}