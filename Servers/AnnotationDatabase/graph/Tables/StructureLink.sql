CREATE TABLE [graph].[StructureLink] (
    [Bidirectional] BIT            CONSTRAINT [DF_StructureLink_Bidirectional] DEFAULT ((0)) NOT NULL,
    [Tags]          XML            NULL,
    [Username]      NVARCHAR (254) CONSTRAINT [DF_StructureLink_Username] DEFAULT (N'') NOT NULL,
    [Created]       DATETIME       CONSTRAINT [DF_StructureLink_Created] DEFAULT (getutcdate()) NOT NULL,
    [LastModified]  DATETIME       CONSTRAINT [DF_StructureLink_LastModified] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [EC_StructureLink1] CONNECTION ([graph].[Structure] TO [graph].[Structure]),
    INDEX [GRAPH_UNIQUE_INDEX_9A9CBD8A27D845C88980FC534AFB583E] UNIQUE NONCLUSTERED ([$edge_id])
) AS EDGE;

