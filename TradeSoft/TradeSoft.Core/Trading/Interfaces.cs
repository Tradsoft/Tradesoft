using TradeSoft.Core.Events;

namespace TradeSoft.Core.Trading;

public interface IStrategy
{
    void OnMarketEvent(MarketEvent marketEvent);
}

public interface IRiskManager
{
    void Validate(MarketEvent marketEvent);
}

public interface IBroker
{
    void OnOrder(MarketEvent marketEvent);
}