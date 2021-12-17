CREATE TABLE [graph].[StructureAttachLocation] (
    [FromStructureID] BIGINT     NOT NULL,
    [ToStructureID]   BIGINT     NOT NULL,
    [Distance]        FLOAT (53) NOT NULL,
    CONSTRAINT [PK_StructureAttachLocation] PRIMARY KEY CLUSTERED ([$edge_id] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [EC_StructureAttachLocation1] CONNECTION ([graph].[Location] TO [graph].[Location]),
    CONSTRAINT [FK_StructureAttachLocation_FromStructure1] FOREIGN KEY ([FromStructureID]) REFERENCES [graph].[Structure] ([ID]) ON DELETE CASCADE,
    CONSTRAINT [FK_StructureAttachLocation_ToStructure1] FOREIGN KEY ([ToStructureID]) REFERENCES [graph].[Structure] ([ID])
) AS EDGE;

