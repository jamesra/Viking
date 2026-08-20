CREATE TABLE [dbo].[DeletedLocationLinks] (
    [A]         BIGINT   NOT NULL,
    [B]         BIGINT   NOT NULL,
    [AZ]        BIGINT   NULL,
    [BZ]        BIGINT   NULL,
    [DeletedOn] DATETIME CONSTRAINT [DF_DeletedLocationLinks_DeletedOn] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_DeletedLocationLinks] PRIMARY KEY CLUSTERED ([A] ASC, [B] ASC) WITH (FILLFACTOR = 90)
);


GO
CREATE NONCLUSTERED INDEX [DeletedLocationLinks_DeletedOn]
    ON [dbo].[DeletedLocationLinks]([DeletedOn] ASC) WITH (FILLFACTOR = 90);
