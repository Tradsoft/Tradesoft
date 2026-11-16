namespace TradeSoft.Core.Domain
{
    public class Portfolio
    {
        public double InitialCash { get; }
        public double Balance { get; private set; }
        public List<Position> Positions { get; private set; } = new List<Position>();
        public List<ClosedTrade> History { get; private set; } = new List<ClosedTrade>();
        public double Equity => Balance + Positions.Sum(p => p.UnrealizedPnL);
        public Portfolio(double startingCash)
        {
            InitialCash = startingCash;
            Balance = startingCash;
        }

        public void OnTrade(Trade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));

            if (trade.Quantity <= 0)
                throw new ArgumentException("Quantity must be positive", nameof(trade.Quantity));

            if (trade.Price <= 0)
                throw new ArgumentException("Price must be positive", nameof(trade.Price));

            Balance -= trade.Commission;

            double qtyRemainingToProcess = trade.Quantity;

            var oppositeSide = trade.Side == Side.Buy ? Side.Sell : Side.Buy;

            var oppositePositions = Positions
                .Where(p => p.Side == oppositeSide)
                .OrderBy(p => p.EntryTime)
                .ToList();

            foreach (var pos in oppositePositions)
            {
                if (qtyRemainingToProcess <= 0) 
                    break;

                double qtyToClose = Math.Min(pos.Quantity, qtyRemainingToProcess);

                double grossPnL = 0;
                if (pos.Side == Side.Buy)
                    grossPnL = (trade.Price - pos.EntryPrice) * qtyToClose;
                else
                    grossPnL = (pos.EntryPrice - trade.Price) * qtyToClose;

                Balance += grossPnL;

                double entryCommPart = pos.Quantity > 0 ? pos.EntryCommission * (qtyToClose / pos.Quantity) : 0;
                pos.EntryCommission -= entryCommPart;
                double exitCommPart = trade.Commission * (qtyToClose / trade.Quantity);

                var closedTrade = new ClosedTrade
                {
                    Symbol = pos.Symbol,
                    Direction = pos.Side,
                    EntryPrice = pos.EntryPrice,
                    ExitPrice = trade.Price,
                    Quantity = qtyToClose,
                    EntryTime = pos.EntryTime,
                    ExitTime = new DateTime(trade.Timestamp),
                    PnL = grossPnL,
                    Commission = entryCommPart + exitCommPart
                };
                History.Add(closedTrade);

                pos.Quantity -= qtyToClose;
                qtyRemainingToProcess -= qtyToClose;

                if (pos.Quantity <= 0.000001)
                {
                    Positions.Remove(pos);
                }
            }

            if (qtyRemainingToProcess > 0)
            {
                var newPos = new Position
                {
                    Id = trade.OrderId,
                    Symbol = trade.Symbol,
                    Side = trade.Side,
                    Quantity = qtyRemainingToProcess,
                    EntryPrice = trade.Price,
                    EntryTime = new DateTime(trade.Timestamp),
                    CurrentPrice = trade.Price,
                    EntryCommission = trade.Commission * (qtyRemainingToProcess / trade.Quantity)
                };
                Positions.Add(newPos);
            }
        }

        public void MarkToMarket(double currentPrice)
        {
            foreach (var pos in Positions) pos.CurrentPrice = currentPrice;
        }
    }
}