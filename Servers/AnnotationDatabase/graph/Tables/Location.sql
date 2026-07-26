CREATE TABLE [graph].[Location] (
    [ID]           BIGINT           IDENTITY (1, 1) NOT NULL,
    [ParentID]     BIGINT           NOT NULL,
    [Z]            BIGINT           NOT NULL,
    [Closed]       BIT              NOT NULL,
    [Version]      ROWVERSION       NOT NULL,
    [Overlay]      VARBINARY (MAX)  NULL,
    [Tags]         XML              NULL,
    [Terminal]     BIT              NOT NULL,
    [OffEdge]      BIT              NOT NULL,
    [TypeCode]     SMALLINT         NOT NULL,
    [LastModified] DATETIME         NOT NULL,
    [Created]      DATETIME         NOT NULL,
    [Username]     NVARCHAR (254)   NOT NULL,
    [MosaicShape]  [sys].[geometry] NOT NULL,
    [VolumeShape]  [sys].[geometry] NOT NULL,
    [X]            AS               (isnull([MosaicShape].[STCentroid]().STX,isnull([MosaicShape].[STX],(0)))) PERSISTED NOT NULL,
    [Y]            AS               (isnull([MosaicShape].[STCentroid]().STY,isnull([MosaicShape].[STY],(0)))) PERSISTED NOT NULL,
    [VolumeX]      AS               (isnull([VolumeShape].[STCentroid]().STX,isnull([VolumeShape].[STX],isnull([VolumeShape].[STEnvelope]().STCentroid().STX,(0))))) PERSISTED NOT NULL,
    [VolumeY]      AS               (isnull([VolumeShape].[STCentroid]().STY,isnull([VolumeShape].[STY],isnull([VolumeShape].[STEnvelope]().STCentroid().STY,(0))))) PERSISTED NOT NULL,
    [Width]        FLOAT (53)       NULL,
    [Radius]       AS               (case [MosaicShape].[STDimension]() when (0) then (0) when (1) then [MosaicShape].[STLength]()/(2.0) when (2) then sqrt([MosaicShape].[STArea]()/pi())  end) PERSISTED NOT NULL,
    CONSTRAINT [PK_Location] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [FK_Graph_Location_Structure] FOREIGN KEY ([ParentID]) REFERENCES [graph].[Structure] ([ID]) ON DELETE CASCADE,
    INDEX [GRAPH_UNIQUE_INDEX_51C868C7C22C451CA44C140ED718B8D6] UNIQUE NONCLUSTERED ([$node_id])
) AS NODE;


GO
CREATE SPATIAL INDEX [Graph_VolumeShape_Index]
    ON [graph].[Location] ([VolumeShape])
    WITH  (
            BOUNDING_BOX = (XMAX = 150000, XMIN = 0, YMAX = 150000, YMIN = 0),
            CELLS_PER_OBJECT = 16
          );

