CREATE TABLE [dbo].[DeletedStructures] (
    [ID]        BIGINT   NOT NULL,
    [DeletedOn] DATETIME CONSTRAINT [DF_DeletedStructures_DeletedOn] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_DeletedStructures] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90)
);


GO
CREATE NONCLUSTERED INDEX [DeletedStructures_DeletedOn]
    ON [dbo].[DeletedStructures]([DeletedOn] ASC) WITH (FILLFACTOR = 90);

