using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Execution
{
    public class SimulatedBroker : Handler
    {
        private readonly long _latencyTicks = TimeSpan.FromMilliseconds(20).Ticks; // Latence simulée de 20 ms

        // File d'attente pour simuler le trajet réseau 
        private readonly Queue<(long ArrivalTime, Order Order)> _networkQueue = new();

        // Carnet d'ordres interne (Ordres arrivés à la bourse et en attente de prix)
        private readonly List<Order> _activeOrders = new();

        protected override void OnProcess(TickContext context)
        {
            long currentKw = context.Tick.Timestamp;

            // 1. GESTION DES NOUVEAUX ORDRES (Strategy -> Network)
            foreach (var order in context.NewOrders)
            {
                _networkQueue.Enqueue((currentKw + _latencyTicks, order));
            }

            // 2. RECEPTION RESEAU (Network -> Order Book)
            while (_networkQueue.Count > 0 && _networkQueue.Peek().ArrivalTime <= currentKw)
            {
                var item = _networkQueue.Dequeue();
                _activeOrders.Add(item.Order);
            }

            // 3. MATCHING ENGINE (Order Book -> Trade)
            // CORRECTION FIFO : On parcourt du début (0 = Ancien) à la fin (Count = Récent)
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                var order = _activeOrders[i];
                bool executed = false;
                double fillPrice = 0;

                // Logique simplifiée pour Market Order (Exécution immédiate au prix dispo)
                if (order.Type == OrderType.Market)
                {
                    // Si on achète, on paie le Ask. Si on vend, on tape le Bid.
                    fillPrice = (order.Side == Side.Buy) ? context.Tick.Ask : context.Tick.Bid;
                    executed = true;
                }
                // Logique pour Limit Order (Exécution si le prix traverse notre limite)
                else if (order.Type == OrderType.Limit)
                {
                    if (order.Side == Side.Buy && context.Tick.Ask <= order.Price)
                    {
                        fillPrice = order.Price; 
                        executed = true;
                    }
                    else if (order.Side == Side.Sell && context.Tick.Bid >= order.Price)
                    {
                        fillPrice = order.Price;
                        executed = true;
                    }
                }

                if (executed)
                {
                    var trade = new Trade
                    {
                        OrderId = order.Id,
                        Timestamp = currentKw,
                        Side = order.Side,
                        Quantity = order.Quantity,
                        Price = fillPrice,
                        Commission = 2.5 // Commission fixe pour la simulation à titre d'exemple
                    };

                    context.Trades.Add(trade);

                    _activeOrders.RemoveAt(i);
                    i--;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(trade.ToString());
                    Console.ResetColor();
                }
            }
        }
    }
}