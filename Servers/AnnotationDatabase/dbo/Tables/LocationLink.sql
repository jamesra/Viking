CREATE TABLE [dbo].[LocationLink] (
    [A]        BIGINT         NOT NULL,
    [B]        BIGINT         NOT NULL,
    [Username] NVARCHAR (254) CONSTRAINT [DF_LocationLink_Username] DEFAULT (N'') NOT NULL,
    [Created]  DATETIME       CONSTRAINT [DF_LocationLink_Created] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_LocationLink] PRIMARY KEY CLUSTERED ([A] ASC, [B] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [chk_LocationLink_Self] CHECK ([A]<>[B]),
    CONSTRAINT [FK_LocationLink_Location] FOREIGN KEY ([A]) REFERENCES [dbo].[Location] ([ID]),
    CONSTRAINT [FK_LocationLink_Location1] FOREIGN KEY ([B]) REFERENCES [dbo].[Location] ([ID])
);


GO
CREATE NONCLUSTERED INDEX [a]
    ON [dbo].[LocationLink]([A] ASC)
    INCLUDE([B]) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [b]
    ON [dbo].[LocationLink]([B] ASC)
    INCLUDE([A]) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [LocationLink_A_B_Username_Created]
    ON [dbo].[LocationLink]([A] ASC, [B] ASC);


GO
CREATE NONCLUSTERED INDEX [LocationLink_B_A_Username_Created]
    ON [dbo].[LocationLink]([B] ASC, [A] ASC);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The convention is that A is always less than B', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LocationLink', @level2type = N'COLUMN', @level2name = N'A';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Last username to modify the row', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LocationLink', @level2type = N'COLUMN', @level2name = N'Username';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Row Creation Date', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LocationLink', @level2type = N'COLUMN', @level2name = N'Created';

