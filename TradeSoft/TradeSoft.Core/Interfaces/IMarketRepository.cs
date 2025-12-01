using TradeSoft.Core.Entities;

namespace TradeSoft.Core.Interfaces
{
    public interface IMarketRepository
    {
        void InitializeDatabase();
        void SaveTicks(List<MarketQuote> ticks);
        List<MarketQuote> GetAllTicks();
    }
}