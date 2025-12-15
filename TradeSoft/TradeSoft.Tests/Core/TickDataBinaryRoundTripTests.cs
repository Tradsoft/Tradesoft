using System;
using System.IO;
using System.Runtime.InteropServices;
using TradeSoft.Core.MarketData;
using Xunit;

public class TickDataBinaryRoundtripTests
{
    [Fact]
    public void TickData_Roundtrip_File_ShouldPreserveAllFields()
    {
        // Arrange
        var tmpFile = Path.Combine(Path.GetTempPath(), $"ticks_{Guid.NewGuid():N}.bin");

        var expected = new TickData(
            timestamp: 638400123456789012L,
            bid: 1.08456,
            ask: 1.08461,
            volume: 1234567890123L,
            flags: TickFlags.Quote | TickFlags.Simulated
        );

        try
        {
            // Act - write
            using (var fs = new FileStream(tmpFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                WriteTick(bw, expected);
            }

            // Act - read
            TickData actual;
            using (var fs = new FileStream(tmpFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                actual = ReadTick(br);
                Assert.Equal(fs.Length, fs.Position); // on a tout lu
            }

            // Assert (champ par champ)
            Assert.Equal(expected.Timestamp, actual.Timestamp);
            Assert.Equal(expected.Bid, actual.Bid);
            Assert.Equal(expected.Ask, actual.Ask);
            Assert.Equal(expected.Volume, actual.Volume);
            Assert.Equal(expected.Flags, actual.Flags);

            // Assert taille struct (contrat)
            Assert.Equal(33, Marshal.SizeOf<TickData>());
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    private static void WriteTick(BinaryWriter w, TickData t)
    {
        w.Write(t.Timestamp);
        w.Write(t.Bid);
        w.Write(t.Ask);
        w.Write(t.Volume);
        w.Write((byte)t.Flags);
    }

    private static TickData ReadTick(BinaryReader r)
    {
        var ts = r.ReadInt64();
        var bid = r.ReadDouble();
        var ask = r.ReadDouble();
        var vol = r.ReadInt64();
        var flags = (TickFlags)r.ReadByte();
        return new TickData(ts, bid, ask, vol, flags);
    }
}
