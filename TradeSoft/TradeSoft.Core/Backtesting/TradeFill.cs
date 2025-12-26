using System;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Backtesting;

/// <summary>
/// Un fill "comptable" reçu par le broker (Filled ou PartiallyFilled).
/// </summary>
public readonly record struct TradeFill(
    Guid CommandId,
    string Symbol,
    OrderSide Side,
    ExecutionStatus Status,
    decimal Quantity,
    decimal Price,
    DateTimeOffset Timestamp
);
