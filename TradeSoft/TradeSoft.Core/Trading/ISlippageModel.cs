using TradeSoft.Core.MarketData;

namespace TradeSoft.Core.Trading;

public interface ISlippageModel
{
    /// <summary>
    /// Ajuste le prix d'exécution selon volume/qty (impact).
    /// basePrice = Ask pour BUY / Bid pour SELL.
    /// </summary>
    decimal GetMaxFillQuantity(in TickData tick);

    decimal Apply(
        string symbol,
        OrderSide side,
        decimal requestedQty,
        decimal fillQty,
        in TickData tick,
        decimal basePrice);
}
