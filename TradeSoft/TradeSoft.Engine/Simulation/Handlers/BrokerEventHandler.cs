using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class BrokerEventHandler : IEventHandler<MarketEvent>
{
    private readonly IBroker? _broker;

    public BrokerEventHandler(IBroker? broker)
    {
        _broker = broker;
    }

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (data.ExecutionReport is null)
            return;
        _broker?.OnExecutionReport(data);
    }
}