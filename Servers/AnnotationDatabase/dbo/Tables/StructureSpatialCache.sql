CREATE TABLE [dbo].[StructureSpatialCache] (
    [ID]           BIGINT           NOT NULL,
    [BoundingRect] [sys].[geometry] NOT NULL,
    [Area]         FLOAT (53)       CONSTRAINT [StructureSpatialCache_Area_Default] DEFAULT ((0)) NOT NULL,
    [Volume]       FLOAT (53)       CONSTRAINT [StructureSpatialCache_Volume_Default] DEFAULT ((0)) NOT NULL,
    [MaxDimension] INT              CONSTRAINT [StructureSpatialCache_MaxDimension_Default] DEFAULT ((0)) NOT NULL,
    [MinZ]         FLOAT (53)       NOT NULL,
    [MaxZ]         FLOAT (53)       NOT NULL,
    [ConvexHull]   [sys].[geometry] NULL,
    [LastModified] DATETIME         NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC),
    FOREIGN KEY ([ID]) REFERENCES [dbo].[Structure] ([ID]) ON DELETE CASCADE
);

