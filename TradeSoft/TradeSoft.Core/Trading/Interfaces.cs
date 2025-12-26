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
    void OnTick(in TickData tick);
    void OnExecutionReport(ExecutionReport report);
}

public interface IMatchingEngine
{
    void Process(MarketEvent e);
}