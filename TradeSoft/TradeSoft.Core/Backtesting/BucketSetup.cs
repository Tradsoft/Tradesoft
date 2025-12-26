namespace TradeSoft.Core.Backtesting;

public enum BucketingMode
{
    /// <summary>On prend le dernier snapshot observé dans le bucket.</summary>
    Last = 0,

    /// <summary>On prend le premier snapshot observé dans le bucket.</summary>
    First = 1
}

public readonly record struct BucketSpec(long Size, BucketingMode Mode = BucketingMode.Last)
{
    public bool IsValid => Size > 0;
}
