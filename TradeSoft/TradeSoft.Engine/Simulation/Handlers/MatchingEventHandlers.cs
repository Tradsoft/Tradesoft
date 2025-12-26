using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class MatchingEventHandler : IEventHandler<MarketEvent>
{
    private readonly IMatchingEngine? _matching;

    public MatchingEventHandler(IMatchingEngine? matching) => _matching = matching;

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (_matching is null) return;
        if (data.IsRejected) return;
        if(data.Tick is null && data.Order is null) return;
        _matching.Process(data);
    }

}
