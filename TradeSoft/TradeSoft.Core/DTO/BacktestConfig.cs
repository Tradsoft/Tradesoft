using TradeSoft.Core.Domain;

namespace TradeSoft.Core.DTO
{
    public class BacktestConfig
    {
        public string Symbol { get; set; } = "EURUSD";
        public AssetType AssetType { get; set; } = AssetType.Spot;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Timeframe { get; set; }

        public double InitialCash { get; set; }
        public string Currency { get; set; } = "USD";
        public double Leverage { get; set; } = 30.0;

        public StrategyType Strategy { get; set; }
        public Dictionary<string, object> StrategyParameters { get; set; } = new();

        public double CommissionPerLot { get; set; } = 2.5;
        public CommissionType CommissionType { get; set; } = CommissionType.Fixed;
        public double SlippagePips { get; set; } = 0.0;
        public double TickSize { get; set; } = 0.0001;

        public double MinLotSize { get; set; } = 100.0;
        public double LotSize { get; set; } = 100000.0;

        public double MaxRiskPerTradePercent { get; set; } = 1.0;
        public double MaxDrawdownPercent { get; set; } = 10.0;
        public int MaxConcurrentPositions { get; set; } = 5;
        public double MaxDailyLossPercent { get; set; } = 5.0;
        public double MinRiskRewardRatio { get; set; } = 1.0;

        public double DefaultRiskNoSL { get; set; } = 0.02; 
        public double DefaultLeverageNoSL { get; set; } = 5.0; 
    }
    public class BacktestResult
    {
        public double InitialCash { get; set; }
        public double FinalEquity { get; set; }
        public double NetProfit { get; set; }
        public double ReturnPercent { get; set; }
        public int TotalTrades { get; set; }
        public double WinRate { get; set; }
        public double ProfitFactor { get; set; }
        public double MaxDrawdownPercent { get; set; }
        public List<TradeResultItem> Trades { get; set; } = new();
        public List<EquityPoint> EquityCurve { get; set; } = new();
        public List<Bar> Bars { get; set; } = new();
    }

    public class TradeResultItem
    {
        public long Id { get; set; }
        public DateTime Time { get; set; }
        public string Side { get; set; } = string.Empty;
        public double Price { get; set; }
        public double Quantity { get; set; }
        public double PnL { get; set; }
    }

    public readonly record struct EquityPoint(DateTime Time, double Value);
}