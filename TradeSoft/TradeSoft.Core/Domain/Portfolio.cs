namespace TradeSoft.Core.Domain
{
    public class Portfolio
    {
        public double InitialCash { get; }
        public double Cash { get; private set; }
        public double Position { get; private set; }

        // Valeur latente (Cash + valeur de revente immédiate de la position)
        public double Equity { get; private set; }

        public Portfolio(double startingCash)
        {
            InitialCash = startingCash;
            Cash = startingCash;
        }

        public void OnTrade(Trade trade)
        {
            // Si on achète, le Cash diminue. Si on vend, le Cash augmente.
            // (Approche simplifiée sans gestion de marge pour l'instant)
            if (trade.Side == Side.Buy)
            {
                Cash -= (trade.Price * trade.Quantity) + trade.Commission;
                Position += trade.Quantity;
            }
            else
            {
                Cash += (trade.Price * trade.Quantity) - trade.Commission;
                Position -= trade.Quantity;
            }
        }

        public void MarkToMarket(double currentPrice)
        {
            Equity = Cash + (Position * currentPrice);
        }
    }
}