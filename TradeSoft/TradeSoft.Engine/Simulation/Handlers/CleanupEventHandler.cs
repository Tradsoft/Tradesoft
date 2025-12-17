using Disruptor;
using TradeSoft.Core.Events;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class CleanupEventHandler : IEventHandler<MarketEvent>
{
    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (endOfBatch)
        {
            data.Reset();
        }
    }
}