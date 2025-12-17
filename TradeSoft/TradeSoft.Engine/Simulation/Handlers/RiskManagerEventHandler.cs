using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class RiskManagerEventHandler : IEventHandler<MarketEvent>
{
    private readonly IRiskManager? _riskManager;

    public RiskManagerEventHandler(IRiskManager? riskManager)
    {
        _riskManager = riskManager;
    }

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        _riskManager?.Validate(data);
    }
}