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

        var count = data.ReportCount;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var report = data.GetReport(i);
            if (report is null) continue;

            _broker.OnExecutionReport(report);
        }
    }
}