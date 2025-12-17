namespace TradeSoft.Core.Trading;

public sealed class ExecutionReport
{
    public ExecutionReport(
        Guid commandId,
        ExecutionStatus status,
        decimal filledQuantity,
        decimal? averagePrice = null,
        string? message = null,
        DateTimeOffset? timestamp = null)
    {
        CommandId = commandId;
        Status = status;
        FilledQuantity = filledQuantity;
        AveragePrice = averagePrice;
        Message = message;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }

    public Guid CommandId { get; }
    public ExecutionStatus Status { get; }
    public decimal FilledQuantity { get; }
    public decimal? AveragePrice { get; }
    public string? Message { get; }
    public DateTimeOffset Timestamp { get; }
}

public enum ExecutionStatus
{
    New = 0,
    PartiallyFilled = 1,
    Filled = 2,
    Rejected = 3,
    Cancelled = 4
}