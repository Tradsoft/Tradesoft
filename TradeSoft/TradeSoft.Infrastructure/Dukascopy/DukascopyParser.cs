using SharpCompress.Compressors.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using TradeSoft.Core.MarketData.Entities;

namespace TradeSoft.Infrastructure.Dukascopy
{
    public static class DukascopyParser
    {
        public static List<TickData> Parse(byte[] compressedBytes, DateTime hourStart, string symbol)
        {
            var quotes = new List<TickData>();

            if (compressedBytes == null || compressedBytes.Length < 13)
                return quotes; // Fichier vide ou trop petit

            // Extraction des propriétés LZMA (5 premiers octets)
            byte[] lzmaProperties = new byte[5];
            Array.Copy(compressedBytes, 0, lzmaProperties, 0, 5);

            // On saute 13 octets (Header Dukascopy)
            int headerSize = 13;

            using (var inputStream = new MemoryStream(compressedBytes, headerSize, compressedBytes.Length - headerSize))
            using (var lzmaStream = new LzmaStream(lzmaProperties, inputStream))
            using (var reader = new BinaryReader(lzmaStream))
            {
                try
                {
                    while (true)
                    {
                        uint timeOffset = ReadBigEndianUInt32(reader);
                        uint askInt = ReadBigEndianUInt32(reader);
                        uint bidInt = ReadBigEndianUInt32(reader);
                        float askVol = ReadBigEndianFloat(reader);
                        float bidVol = ReadBigEndianFloat(reader);

                        decimal pointValue = 100000m;

                        var quote = new TickData
                        {
                            Symbol = symbol,
                            Timestamp = hourStart.AddMilliseconds(timeOffset),
                            Ask = askInt / pointValue,
                            Bid = bidInt / pointValue,
                            Volume = (long)(askVol + bidVol)
                        };

                        // Filtre Anti-Bruit (Protection corruption fin de fichier)
                        if (quote.Ask > 2.0m || quote.Bid < 0.5m) break;

                        quotes.Add(quote);
                    }
                }
                catch (EndOfStreamException) { /* Fin normale */ }
                catch (Exception)
                {
                    if (quotes.Count == 0) throw;
                }
            }
            return quotes;
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static float ReadBigEndianFloat(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, 0);
        }
    }
}