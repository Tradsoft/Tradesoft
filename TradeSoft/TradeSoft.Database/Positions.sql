CREATE TABLE [dbo].[Positions]
(
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Symbol] NVARCHAR(50) NOT NULL,
    [Quantity] DECIMAL(18, 5) NOT NULL,
    [AverageEntryPrice] DECIMAL(18, 5) NOT NULL,
    [UnrealizedPnl] DECIMAL(18, 5) NOT NULL,
)
GO
