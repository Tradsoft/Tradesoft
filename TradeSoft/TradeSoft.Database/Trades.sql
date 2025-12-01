CREATE TABLE [dbo].[Trades]
(
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [OrderId] BIGINT NOT NULL,
    [ExecutionTime] DATETIME2(3) NOT NULL,
    [Symbol] NVARCHAR(50) NOT NULL,
    [Side] NVARCHAR(50) NOT NULL,
    [Quantity] DECIMAL(18, 5) NOT NULL,
    [ExecutionPrice] DECIMAL(18, 5) NOT NULL, 
    CONSTRAINT [FK_Trades_ToOrdersId] FOREIGN KEY ([OrderId]) REFERENCES [Dbo].[Orders]([Id]), 
    [Commission] DECIMAL(18, 5) NOT NULL,
    [Pnl] DECIMAL(18, 5) NOT NULL,





)

GO
