namespace TradeSoft.Core.Trading;

public sealed class OrderCommand
{
    public OrderCommand(
        Guid commandId,
        string symbol,
        OrderSide side,
        decimal quantity,
        OrderType type,
        OrderTimeInForce timeInForce,
        decimal? limitPrice = null,
        DateTimeOffset? timestamp = null)
    {
        CommandId = commandId;
        Symbol = symbol;
        Side = side;
        Quantity = quantity;
        Type = type;
        TimeInForce = timeInForce;
        LimitPrice = limitPrice;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public Guid CommandId { get; }
    public string Symbol { get; }
    public OrderSide Side { get; }
    public decimal Quantity { get; }
    public OrderType Type { get; }
    public OrderTimeInForce TimeInForce { get; }
    public decimal? LimitPrice { get; }
    public DateTimeOffset Timestamp { get; }
}

public enum OrderSide
{
    Buy = 0,
    Sell = 1
}

public enum OrderType
{
    Market = 0,
    Limit = 1
}

public enum OrderTimeInForce
{
    Day = 0,
    Gtc = 1,
    ImmediateOrCancel = 2
}