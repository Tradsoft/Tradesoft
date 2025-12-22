using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class BrokerReportHandler : IEventHandler<MarketEvent>
{
    private readonly IBroker? _broker;

    public BrokerReportHandler(IBroker? broker) => _broker = broker;

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (_broker is null) return;
        if (data.ExecutionReport is null) return;

        var report = data.ExecutionReport; 
        _broker.OnExecutionReport(report);
    }
}
