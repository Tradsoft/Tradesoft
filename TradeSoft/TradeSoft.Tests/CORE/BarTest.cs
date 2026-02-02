using System;
using TradeSoft.Core.Domain;
using Xunit;

public class BarTest
{
    [Fact]
    public void CreateBar_WithValidData_ShouldCreateBarCorrectly()
    {
        // 🟦 Arrange : données valides
        var open = 1.10;
        var high = 1.15;
        var low = 1.05;
        var close = 1.12;
        var timestamp = DateTime.Now;

        // 🟨 Act : création de l’entité
        var bar = new Bar
        {
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Timestamp = timestamp
        };

        // 🟩 Assert : vérifications
        Assert.NotNull(bar);
        Assert.Equal(open, bar.Open);
        Assert.Equal(high, bar.High);
        Assert.Equal(low, bar.Low);
        Assert.Equal(close, bar.Close);
        Assert.Equal(timestamp, bar.Timestamp);
    }
}

