using FluentAssertions;
using System.Runtime.InteropServices;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.MarketData.Entities;
using Xunit;

public class TickDataTests
{
    [Fact]
    public void TickData_Size_ShouldBe_33_Bytes()
    {
        Marshal.SizeOf<TickData>().Should().Be(33);
    }
}
