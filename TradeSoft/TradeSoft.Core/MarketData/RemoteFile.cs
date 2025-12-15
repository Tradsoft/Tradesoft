namespace TradeSoft.Core.MarketData;

public sealed record RemoteFile(
    string Key,            // ex: "EURUSD/EURUSD_2019.bin" (clé remote)
    string LocalFileName,  // ex: "EURUSD_2019.bin"
    string Sha256Hex,      // hex uppercase/lowercase OK
    long SizeBytes         
);
