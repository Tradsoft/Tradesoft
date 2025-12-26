using TradeSoft.Core.MarketData;

namespace TradeSoft.Core.Trading;

public interface ILatencyModel
{
    /// <summary>
    /// Retourne une latence en "ticks de timestamp" (long).
    /// Exemple : 2 => un ordre envoyé à ts=3 devient exécutable à ts=5.
    /// </summary>
    long GetLatencyTicks(in OrderCommand command, in TickData tick);
}
