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
        public double Leverage { get; set; } = 1.0; 

        public StrategyType Strategy { get; set; }
        public Dictionary<string, object> StrategyParameters { get; set; } = new();

        public double CommissionPerLot { get; set; } = 2.5;
        public CommissionType CommissionType { get; set; } = CommissionType.Fixed;
        public double SlippagePips { get; set; } = 0.0;

        public double MaxRiskPerTradePercent { get; set; } = 1.0; 
        public double MaxDrawdownPercent { get; set; } = 10.0;    
    }

    public enum AssetType { Spot, Future, CFDs }
    public enum CommissionType { Fixed, Percent }
    public enum StrategyType { Breakout, MAC, Random, RSI, MeanReversion }


    public class BacktestResult
    {
        public double InitialCash { get; set; }
        public double FinalEquity { get; set; }
        public double NetProfit { get; set; }
        public double ReturnPercent { get; set; }
        public int TotalTrades { get; set; }
        public List<TradeResultItem> Trades { get; set; } = new();
        public List<EquityPoint> EquityCurve { get; set; } = new();
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
