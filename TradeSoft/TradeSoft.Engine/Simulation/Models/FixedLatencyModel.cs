using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Models;

public sealed class FixedLatencyModel : ILatencyModel
{
    private readonly long _latencyTicks;

    public FixedLatencyModel(long latencyTicks) => _latencyTicks = Math.Max(0, latencyTicks);

    public long GetLatencyTicks(in OrderCommand command, in TickData tick) => _latencyTicks;
}
