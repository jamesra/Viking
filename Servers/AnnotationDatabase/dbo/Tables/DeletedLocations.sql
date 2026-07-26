CREATE TABLE [dbo].[DeletedLocations] (
    [ID]        BIGINT   NOT NULL,
    [DeletedOn] DATETIME CONSTRAINT [DF_DeletedLocations_DeletedOn] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_DeletedLocations] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90)
);


GO
CREATE NONCLUSTERED INDEX [DeletedOn]
    ON [dbo].[DeletedLocations]([DeletedOn] ASC) WITH (FILLFACTOR = 90);

