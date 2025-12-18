using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;

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
    void OnExecutionReport(MarketEvent marketEvent);
}

public interface IMatchingEngine
{
    ExecutionReport Execute(OrderCommand command, TickData? tick);
}