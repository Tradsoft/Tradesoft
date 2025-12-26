using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation.Models;

/// <summary>
/// v4+ :
/// - Budget volume par tick = tick.Volume * maxParticipation
/// - Impact (pips) = impactPipsPer1x * (fillQty / maxFillQty)
/// BUY => px augmente, SELL => px baisse.
/// </summary>
public sealed class VolumeShareSlippageModel : ISlippageModel
{
    private readonly decimal _maxParticipation;   // ex: 0.05m (5%) mid-frequency réaliste
    private readonly decimal _impactPipsPer1x;    // ex: 0.8m  (0.8 pip quand tu manges 100% du budget de tick)
    private readonly decimal _pipSize;            // EURUSD pip = 0.0001m

    public VolumeShareSlippageModel(decimal maxParticipation, decimal impactPipsPer1x, decimal pipSize = 0.0001m)
    {
        if (maxParticipation <= 0m || maxParticipation > 1m) throw new ArgumentOutOfRangeException(nameof(maxParticipation));
        if (impactPipsPer1x < 0m) throw new ArgumentOutOfRangeException(nameof(impactPipsPer1x));
        if (pipSize <= 0m) throw new ArgumentOutOfRangeException(nameof(pipSize));

        _maxParticipation = maxParticipation;
        _impactPipsPer1x = impactPipsPer1x;
        _pipSize = pipSize;
    }

    public decimal GetMaxFillQuantity(in TickData tick)
    {
        var v = (decimal)tick.Volume;
        if (v <= 0m) return 0m;

        return v * _maxParticipation;
    }

    public decimal Apply(
        string symbol,
        OrderSide side,
        decimal requestedQty,
        decimal fillQty,
        in TickData tick,
        decimal basePrice)
    {
        var maxFill = GetMaxFillQuantity(in tick);
        if (maxFill <= 0m) return basePrice;

        // ratio=1 quand on consomme tout le budget de tick (fillQty=maxFill)
        var ratio = Math.Clamp(fillQty / maxFill, 0m, 1m);

        var impact = _impactPipsPer1x * _pipSize * ratio;

        return side == OrderSide.Buy
            ? basePrice + impact
            : basePrice - impact;
    }
}
