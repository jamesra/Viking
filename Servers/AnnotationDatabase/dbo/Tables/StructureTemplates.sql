CREATE TABLE [dbo].[StructureTemplates] (
    [ID]              BIGINT         IDENTITY (1, 1) NOT NULL,
    [Name]            CHAR (64)      NOT NULL,
    [StructureTypeID] BIGINT         NOT NULL,
    [StructureTags]   NVARCHAR (MAX) NOT NULL,
    [Version]         ROWVERSION     NOT NULL,
    CONSTRAINT [PK_StructureTemplates] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [FK_StructureTemplates_StructureType] FOREIGN KEY ([StructureTypeID]) REFERENCES [dbo].[StructureType] ([ID])
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Name of template', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureTemplates', @level2type = N'COLUMN', @level2name = N'Name';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The structure type which is created when using the template', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureTemplates', @level2type = N'COLUMN', @level2name = N'StructureTypeID';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'The tags to create with the new structure type', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureTemplates', @level2type = N'COLUMN', @level2name = N'StructureTags';

