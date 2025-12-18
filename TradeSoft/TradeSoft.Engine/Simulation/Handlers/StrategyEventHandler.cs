using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class StrategyEventHandler : IEventHandler<MarketEvent>
{
    private readonly IStrategy? _strategy;

    public StrategyEventHandler(IStrategy? strategy)
    {
        _strategy = strategy;
    }

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if(data.Type != MarketEventType.Tick || data.Tick is null)
            return;
        _strategy?.OnMarketEvent(data);
    }
}