using System;
using System.Collections.Generic;
using System.Linq;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Execution
{
    public class SimulatedBroker : Handler
    {
        private readonly BacktestConfig _config;
        private readonly Random _random = new Random();

        private readonly long _latencyTicks = TimeSpan.FromMilliseconds(20).Ticks;
        private readonly Queue<(long ArrivalTime, Order Order)> _networkQueue = new();

        private readonly List<Order> _activeOrders = new();
        private readonly List<Order> _pendingExits = new List<Order>();

        public SimulatedBroker(BacktestConfig config)
        {
            _config = config;
        }

        protected override void OnProcess(TickContext context)
        {
            long currentKw = context.Tick.Timestamp;
            double bid = context.Tick.Bid;
            double ask = context.Tick.Ask;

            // 1. LATENCE RÉSEAU
            foreach (var order in context.NewOrders)
            {
                _networkQueue.Enqueue((currentKw + _latencyTicks, order));
            }
            context.NewOrders.Clear();

            while (_networkQueue.Count > 0 && _networkQueue.Peek().ArrivalTime <= currentKw)
            {
                var item = _networkQueue.Dequeue();
                _activeOrders.Add(item.Order);
            }

            // 2. GESTION SL / TP
            // Correction du risque de null sur exitOrder.Comment dans la gestion SL/TP
            for (int i = _pendingExits.Count - 1; i >= 0; i--)
            {
                var exitOrder = _pendingExits[i];
                bool triggered = false;

                if (!string.IsNullOrEmpty(exitOrder.Comment))
                {
                    if (exitOrder.Side == Side.Sell) // Sortie d'un Long
                    {
                        if (exitOrder.Comment.Contains("SL") && bid <= exitOrder.Price) triggered = true;
                        else if (exitOrder.Comment.Contains("TP") && bid >= exitOrder.Price) triggered = true;
                    }
                    else // Sortie d'un Short
                    {
                        if (exitOrder.Comment.Contains("SL") && ask >= exitOrder.Price) triggered = true;
                        else if (exitOrder.Comment.Contains("TP") && ask <= exitOrder.Price) triggered = true;
                    }
                }

                if (triggered)
                {
                    ExecuteOrder(context, exitOrder);
                    CleanupOCO(exitOrder);
                    _pendingExits.RemoveAt(i);
                }
            }

            // 3. MATCHING ENGINE
            for (int i = 0; i < _activeOrders.Count; i++)
            {
                var order = _activeOrders[i];
                bool triggered = false;
                double? limitPrice = null;

                if (order.Type == OrderType.Market)
                {
                    triggered = true;
                }
                else if (order.Type == OrderType.Limit)
                {
                    if (order.Side == Side.Buy && ask <= order.Price)
                    {
                        triggered = true;
                        limitPrice = order.Price;
                    }
                    else if (order.Side == Side.Sell && bid >= order.Price)
                    {
                        triggered = true;
                        limitPrice = order.Price;
                    }
                }

                if (triggered)
                {
                    ExecuteOrder(context, order, limitPrice);
                    _activeOrders.RemoveAt(i);
                    i--;
                }
            }
        }

        private void ExecuteOrder(TickContext context, Order order, double? forceBasePrice = null)
        {
            double basePrice = forceBasePrice ?? ((order.Side == Side.Buy) ? context.Tick.Ask : context.Tick.Bid);

            double slippageImpact = _config.SlippagePips * _config.TickSize;

            double executedPrice = (order.Side == Side.Buy)
                ? basePrice + slippageImpact
                : basePrice - slippageImpact;

            double commission = 0;
            if (_config.CommissionType == CommissionType.Fixed)
            {
                commission = _config.CommissionPerLot * order.Quantity;
            }
            else
            {
                commission = (executedPrice * order.Quantity) * (_config.CommissionPerLot / 100.0);
            }

            var trade = new Trade
            {
                OrderId = order.Id,
                Timestamp = context.Tick.Timestamp,
                Side = order.Side,
                Quantity = order.Quantity,
                Price = executedPrice,
                Commission = commission
            };

            context.Trades.Add(trade);
            order.IsFilled = true;

            if (!IsExitOrder(order) && (order.StopLoss.HasValue || order.TakeProfit.HasValue))
            {
                RegisterProtections(order);
            }
        }

        private void RegisterProtections(Order parent)
        {
            Side exitSide = parent.Side == Side.Buy ? Side.Sell : Side.Buy;

            if (parent.StopLoss.HasValue)
            {
                _pendingExits.Add(new Order
                {
                    Id = DateTime.UtcNow.Ticks,
                    Side = exitSide,
                    Quantity = parent.Quantity,
                    Price = parent.StopLoss.Value,
                    Type = OrderType.Market,
                    Comment = $"SL_Parent_{parent.Id}"
                });
            }

            if (parent.TakeProfit.HasValue)
            {
                _pendingExits.Add(new Order
                {
                    Id = DateTime.UtcNow.Ticks + 1,
                    Side = exitSide,
                    Quantity = parent.Quantity,
                    Price = parent.TakeProfit.Value,
                    Type = OrderType.Limit,
                    Comment = $"TP_Parent_{parent.Id}"
                });
            }
        }

        private void CleanupOCO(Order triggeredOrder)
        {
            if (string.IsNullOrEmpty(triggeredOrder.Comment)) return;
            var parts = triggeredOrder.Comment.Split('_');
            if (parts.Length < 3) return;
            string parentId = parts[2];
            _pendingExits.RemoveAll(o => o != triggeredOrder && o.Comment != null && o.Comment.Contains(parentId));
        }

        private bool IsExitOrder(Order order)
        {
            return order.Comment != null && (order.Comment.Contains("SL_") || order.Comment.Contains("TP_"));
        }
    }
}