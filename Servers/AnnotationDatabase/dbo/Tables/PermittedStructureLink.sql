CREATE TABLE [dbo].[PermittedStructureLink] (
    [SourceTypeID]  BIGINT NOT NULL,
    [TargetTypeID]  BIGINT NOT NULL,
    [Bidirectional] BIT    NOT NULL,
    CONSTRAINT [PK_PermittedStructureLink] PRIMARY KEY CLUSTERED ([SourceTypeID] ASC, [TargetTypeID] ASC),
    CONSTRAINT [FK_PermittedStructureLink_SourceType] FOREIGN KEY ([SourceTypeID]) REFERENCES [dbo].[StructureType] ([ID]),
    CONSTRAINT [FK_PermittedStructureLink_TargetType] FOREIGN KEY ([TargetTypeID]) REFERENCES [dbo].[StructureType] ([ID]),
    CONSTRAINT [PermittedStructureLink_source_target_unique] UNIQUE NONCLUSTERED ([SourceTypeID] ASC, [TargetTypeID] ASC)
);

