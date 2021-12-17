CREATE TABLE [dbo].[Structure] (
    [ID]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [TypeID]       BIGINT         NOT NULL,
    [Notes]        NVARCHAR (MAX) NULL,
    [Verified]     BIT            CONSTRAINT [DF_StructureBase_Verified] DEFAULT ((0)) NOT NULL,
    [Tags]         XML            NULL,
    [Confidence]   FLOAT (53)     CONSTRAINT [DF_StructureBase_Confidence] DEFAULT ((0.5)) NOT NULL,
    [Version]      ROWVERSION     NOT NULL,
    [ParentID]     BIGINT         NULL,
    [Created]      DATETIME       CONSTRAINT [DF_Structure_Created] DEFAULT (getutcdate()) NOT NULL,
    [Label]        VARCHAR (64)   NULL,
    [Username]     NVARCHAR (254) CONSTRAINT [DF_Structure_Username] DEFAULT (N'') NOT NULL,
    [LastModified] DATETIME       CONSTRAINT [DF_Structure_LastModified] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_StructureBase] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [FK_Structure_Structure] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[Structure] ([ID]),
    CONSTRAINT [FK_StructureBase_StructureType] FOREIGN KEY ([TypeID]) REFERENCES [dbo].[StructureType] ([ID])
);


GO
CREATE NONCLUSTERED INDEX [ParentID]
    ON [dbo].[Structure]([ParentID] ASC) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [TypeID]
    ON [dbo].[Structure]([TypeID] ASC) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [LastModified]
    ON [dbo].[Structure]([LastModified] DESC) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [Structure_ParentID_ID]
    ON [dbo].[Structure]([ParentID] ASC, [ID] ASC);


GO
CREATE STATISTICS [_dta_stat_Structure_ParentID_ID]
    ON [dbo].[Structure]([ParentID], [ID]);


GO
CREATE STATISTICS [_dta_stat_Structure_ID_TypeID]
    ON [dbo].[Structure]([ID], [TypeID]);


GO
CREATE TRIGGER [dbo].[Structure_LastModified] 
			   ON  [dbo].[Structure]
			   FOR UPDATE
			AS 
				Update dbo.[Structure]
				Set LastModified = (SYSUTCDATETIME())
				WHERE ID in (SELECT ID FROM inserted)
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Strings seperated by semicolins', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Tags';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'How certain is it that the structure is what we say it is', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Confidence';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Records last write time', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Version';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'If the structure is contained in a larger structure (Synapse for a cell) this index contains the index of the parent', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'ParentID';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Date the structure was created', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Created';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Additional Label for structure in UI', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Label';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Last username to modify the row', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Structure', @level2type = N'COLUMN', @level2name = N'Username';

