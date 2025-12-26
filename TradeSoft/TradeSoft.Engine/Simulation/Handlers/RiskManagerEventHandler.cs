using Disruptor;
using TradeSoft.Core.Events;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Handlers;

public sealed class RiskManagerEventHandler : IEventHandler<MarketEvent>
{
    private readonly IRiskManager? _riskManager;
    public RiskManagerEventHandler(IRiskManager? riskManager) => _riskManager = riskManager;

    public void OnEvent(MarketEvent data, long sequence, bool endOfBatch)
    {
        if (_riskManager is null) return;
        if (data.Order is null) return;
        if (data.IsRejected) return;
        if (data.HasReports) return; 

        try
        {
            _riskManager.Validate(data);
        }
        catch (Exception ex)
        {
            data.Reject($"RiskManager validation failed: {ex.GetType().Name}");
        }
    }
}
