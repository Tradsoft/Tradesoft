CREATE TABLE [dbo].[Orders]
(
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Timestamp] DATETIME2(3) NOT NULL, 
    [Symbol] NVARCHAR(50) NOT NULL,
    [Side] NVARCHAR(50) NOT NULL,
    [Type] NVARCHAR(50) NOT NULL,
    [Quantity] DECIMAL(18, 5) NOT NULL,
    [LimitedPrice] DECIMAL(18, 5) NULL,
    [StopPrice] DECIMAL(18, 5) NULL,
    [Status] NVARCHAR(50) NOT NULL,
    [RejectReason] NVARCHAR(50) NULL

)

GO
