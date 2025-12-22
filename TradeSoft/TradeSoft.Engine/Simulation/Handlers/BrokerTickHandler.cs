using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class BrokerTickHandler : IEventHandler<MarketEvent>
{
    private readonly IBroker? _broker;

    public BrokerTickHandler(IBroker? broker) => _broker = broker;

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (_broker is null) return;
        if (data.Tick is null) return;

        _broker.OnTick(data.Tick.Value);
    }
}
