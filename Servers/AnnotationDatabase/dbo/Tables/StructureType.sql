CREATE TABLE [dbo].[StructureType] (
    [ID]            BIGINT         IDENTITY (1, 1) NOT NULL,
    [ParentID]      BIGINT         NULL,
    [Name]          NCHAR (128)    NOT NULL,
    [Notes]         NVARCHAR (MAX) NULL,
    [MarkupType]    NCHAR (16)     CONSTRAINT [DF_StructureType_MarkupType] DEFAULT (N'Point') NOT NULL,
    [Tags]          XML            NULL,
    [StructureTags] XML            NULL,
    [Abstract]      BIT            CONSTRAINT [DF_StructureType_Abstract] DEFAULT ((0)) NOT NULL,
    [Color]         INT            CONSTRAINT [DF_StructureType_Color] DEFAULT (0xFFFFFF) NOT NULL,
    [Version]       ROWVERSION     NOT NULL,
    [Code]          NCHAR (16)     CONSTRAINT [DF_StructureType_Code] DEFAULT (N'No Code') NOT NULL,
    [HotKey]        CHAR (1)       CONSTRAINT [DF_StructureType_HotKey] DEFAULT ('\0') NOT NULL,
    [Username]      NVARCHAR (254) CONSTRAINT [DF_StructureType_Username] DEFAULT (N'') NOT NULL,
    [LastModified]  DATETIME       CONSTRAINT [DF_StructureType_LastModified] DEFAULT (getutcdate()) NOT NULL,
    [Created]       DATETIME       CONSTRAINT [DF_StructureType_Created] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_StructureType] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [FK_StructureType_StructureType] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[StructureType] ([ID])
);


GO
CREATE NONCLUSTERED INDEX [ParentID]
    ON [dbo].[StructureType]([ParentID] ASC) WITH (FILLFACTOR = 90);


GO
CREATE TRIGGER [dbo].[StructureType_LastModified] 
			   ON  [dbo].[StructureType]
			   FOR UPDATE
			AS 
				Update dbo.[StructureType]
				Set LastModified = (SYSUTCDATETIME())
				WHERE ID in (SELECT ID FROM inserted)
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Point,Line,Poly', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureType', @level2type = N'COLUMN', @level2name = N'MarkupType';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Strings seperated by semicolins', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureType', @level2type = N'COLUMN', @level2name = N'Tags';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Code used to identify these items in the UI', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureType', @level2type = N'COLUMN', @level2name = N'Code';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Hotkey used to create a structure of this type', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureType', @level2type = N'COLUMN', @level2name = N'HotKey';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Last username to modify the row', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureType', @level2type = N'COLUMN', @level2name = N'Username';

