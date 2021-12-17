CREATE TABLE [dbo].[Location] (
    [ID]           BIGINT           IDENTITY (1, 1) NOT NULL,
    [ParentID]     BIGINT           NOT NULL,
    [Z]            BIGINT           NOT NULL,
    [Closed]       BIT              CONSTRAINT [DF_Location_Closed] DEFAULT ((0)) NOT NULL,
    [Version]      ROWVERSION       NOT NULL,
    [Overlay]      VARBINARY (MAX)  NULL,
    [Tags]         XML              NULL,
    [Terminal]     BIT              CONSTRAINT [DF_Location_Flagged] DEFAULT ((0)) NOT NULL,
    [OffEdge]      BIT              CONSTRAINT [DF_Location_OffEdge] DEFAULT ((0)) NOT NULL,
    [TypeCode]     SMALLINT         CONSTRAINT [DF_Location_TypeCode] DEFAULT ((1)) NOT NULL,
    [LastModified] DATETIME         CONSTRAINT [DF_Location_LastModified] DEFAULT (getutcdate()) NOT NULL,
    [Created]      DATETIME         CONSTRAINT [DF_Location_Created] DEFAULT (getutcdate()) NOT NULL,
    [Username]     NVARCHAR (254)   CONSTRAINT [DF_Location_Username] DEFAULT (N'') NOT NULL,
    [MosaicShape]  [sys].[geometry] NOT NULL,
    [VolumeShape]  [sys].[geometry] NOT NULL,
    [X]            AS               (isnull([MosaicShape].[STCentroid]().STX,isnull([MosaicShape].[STX],(0)))) PERSISTED NOT NULL,
    [Y]            AS               (isnull([MosaicShape].[STCentroid]().STY,isnull([MosaicShape].[STY],(0)))) PERSISTED NOT NULL,
    [VolumeX]      AS               (isnull([VolumeShape].[STCentroid]().STX,isnull([VolumeShape].[STX],isnull([VolumeShape].[STEnvelope]().STCentroid().STX,(0))))) PERSISTED NOT NULL,
    [VolumeY]      AS               (isnull([VolumeShape].[STCentroid]().STY,isnull([VolumeShape].[STY],isnull([VolumeShape].[STEnvelope]().STCentroid().STY,(0))))) PERSISTED NOT NULL,
    [Width]        FLOAT (53)       NULL,
    [Radius]       AS               (case [MosaicShape].[STDimension]() when (0) then (0) when (1) then [MosaicShape].[STLength]()/(2.0) when (2) then sqrt([MosaicShape].[STArea]()/pi())  end) PERSISTED NOT NULL,
    CONSTRAINT [PK_Location] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [chk_Location_Width] CHECK ((0)=[TypeCode] AND [Width] IS NULL OR (1)=[TypeCode] AND [Width] IS NULL OR (2)=[TypeCode] AND [Width] IS NULL OR (3)=[TypeCode] AND [Width] IS NOT NULL OR (4)=[TypeCode] AND [Width] IS NULL OR (5)=[TypeCode] AND [Width] IS NOT NULL OR (6)=[TypeCode] AND [Width] IS NULL OR (7)=[TypeCode] AND [Width] IS NOT NULL),
    CONSTRAINT [FK_Location_StructureBase1] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[Structure] ([ID]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [Z]
    ON [dbo].[Location]([Z] ASC);


GO
CREATE NONCLUSTERED INDEX [ParentID]
    ON [dbo].[Location]([ParentID] ASC) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [LastModified]
    ON [dbo].[Location]([LastModified] ASC) WITH (FILLFACTOR = 90);


GO
CREATE SPATIAL INDEX [VolumeShape_Index]
    ON [dbo].[Location] ([VolumeShape])
    WITH  (
            BOUNDING_BOX = (XMAX = 150000, XMIN = 0, YMAX = 150000, YMIN = 0),
            CELLS_PER_OBJECT = 16
          );


GO
CREATE SPATIAL INDEX [MosaicShape_Index]
    ON [dbo].[Location] ([MosaicShape])
    WITH  (
            BOUNDING_BOX = (XMAX = 150000, XMIN = 0, YMAX = 150000, YMIN = 0),
            CELLS_PER_OBJECT = 16
          );


GO
CREATE STATISTICS [_dta_stat_Location_ParentID_ID_Z]
    ON [dbo].[Location]([ParentID], [ID], [Z]);


GO
CREATE STATISTICS [_dta_stat_Location_Z_ID]
    ON [dbo].[Location]([Z], [ID]);


GO
CREATE STATISTICS [_dta_stat_Location_ID_ParentID]
    ON [dbo].[Location]([ID], [ParentID]);


GO

			 CREATE TRIGGER [dbo].[Location_update] 
				ON  [dbo].[Location]
				FOR UPDATE
				AS 
					Update dbo.Location
					Set LastModified = (SYSUTCDATETIME())
					WHERE ID in (SELECT ID FROM inserted)
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;
GO
CREATE TRIGGER UpdateStructureSpatialCache
			  ON Location
			  AFTER INSERT, UPDATE, DELETE
			as
			BEGIN
				IF TRIGGER_NESTLEVEL() > 1/*this update is coming from some other trigger*/
					return

				SET NOCOUNT ON

				DELETE StructureSpatialCache 
				WHERE StructureSpatialCache.ID IN (SELECT ParentID FROM DELETED Group By ParentID)

				DELETE StructureSpatialCache 
				WHERE StructureSpatialCache.ID IN (SELECT ParentID FROM INSERTED Group By ParentID)	

				INSERT INTO StructureSpatialCache
				SELECT        S.ID as ID,  
							  L.BoundingRect as BoundingRect,
							  [dbo].ufnStructureArea(S.ID) as Area, 
							  [dbo].ufnStructureVolume(S.ID) as Volume, 
							  L.MaxDim as MaxDimension,
							  L.MinZ as MinZ, 
							  L.MaxZ as MaxZ,
							  L.ConvexHull as ConvexHull,
							  [dbo].ufnLastStructureMorphologyModification(S.ID) as LastModified

				FROM Structure S
				INNER JOIN 
					(select L.ParentID, 
					   --Geometry::UnionAggregate(L.VolumeShape) as AggregateShape,
					   Geometry::ConvexHullAggregate(L.VolumeShape) as ConvexHull,
					   Geometry::EnvelopeAggregate(L.VolumeShape) as BoundingRect,
					   max(L.VolumeShape.STDimension()) as MaxDim,
					   min(L.Z) as MinZ,
					   max(L.Z) as MaxZ
				FROM Location L group by L.ParentID) L  ON L.ParentID = S.ID
				INNER JOIN (Select ParentID from INSERTED I group by ParentID) I ON I.ParentID = S.ID /*Must use Group by in the inner join or we create many rows and re-run expensive functions calculating columns for all of them*/
			END

			exec sp_settriggerorder @triggername= 'UpdateStructureSpatialCache', @order='Last', @stmttype = 'UPDATE';  
		
GO
EXECUTE sp_settriggerorder @triggername = N'[dbo].[UpdateStructureSpatialCache]', @order = N'last', @stmttype = N'update';


GO

			 CREATE TRIGGER [dbo].[Location_delete] 
			   ON  [dbo].[Location]
			   FOR DELETE
			 AS 
				INSERT INTO [dbo].[DeletedLocations] (ID)
				SELECT deleted.ID FROM deleted
				
				delete from LocationLink 
					where A in  (SELECT deleted.ID FROM deleted)
						or B in (SELECT deleted.ID FROM deleted)
				
				SET NOCOUNT ON;
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Structure which we belong to', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'ParentID';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Defines whether Vertices form a closed figure (The last vertex connects to the first)', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Closed';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'An image centered on X,Y,Z which specifies which surrounding pixels are part of location', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Overlay';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Set to true if this location is the edge of a structure and cannot be extended.', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Terminal';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'This bit is set if the structure leaves the volume at this location', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'OffEdge';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'0 = Point, 1 = Circle, 2=Ellipse, 3 =PolyLine, 4=Polygon', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'TypeCode';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Date the location was last modified', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'LastModified';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Date the location was created', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Created';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Last username to modify the row', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Username';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Width used for line annotation types', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Width';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Radius, calculated column needed for backwards compatability', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Location', @level2type = N'COLUMN', @level2name = N'Radius';

