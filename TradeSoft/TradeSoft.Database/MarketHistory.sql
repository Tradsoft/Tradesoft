CREATE TABLE [dbo].[MarketHistory]
(
    [Id] BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [Symbol] NVARCHAR(50) NOT NULL, 
    [Timestamp] DATETIME2(3) NOT NULL, 
    [Ask] DECIMAL(18, 5) NOT NULL, 
    [Bid] DECIMAL(18, 5) NOT NULL, 
    [Volume] BIGINT NOT NULL
)

GO

-- Création de l'index directement ici
CREATE NONCLUSTERED INDEX [IX_MarketHistory_Time] ON [dbo].[MarketHistory] ([Timestamp])