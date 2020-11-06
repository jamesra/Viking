IF NOT EXISTS (SELECT 0
               FROM information_schema.schemata 
               WHERE schema_name='graph')
 BEGIN
   print 'Creating graph schema'

   EXEC sp_executesql N'CREATE SCHEMA Graph';
 END
ELSE
 BEGIN
   print 'Deleting existing graph database'
   
   DROP TABLE IF EXISTS graph.[StructuresAttachLocation]

   DROP TABLE IF EXISTS graph.[LocationLink]
    
   DROP TABLE IF EXISTS graph.[StructureLink]
   
   DROP TABLE IF EXISTS graph.[Location]
    
   DROP TABLE IF EXISTS graph.[Structure]
   
 END
go

/*  Structure  */

print 'Creating graph database'
go

print '  Creating structure node table'
go

DROP TABLE IF EXISTS graph.Structure
GO

CREATE TABLE graph.Structure (
	[ID] [bigint] IDENTITY(1,1) NOT NULL,
	[TypeID] [bigint] NOT NULL,
	[Notes] [nvarchar](max) NULL,
	[Verified] [bit] NOT NULL,
	[Tags] [xml] NULL,
	[Confidence] [float] NOT NULL,
	[Version] [timestamp] NOT NULL,
	[ParentID] [bigint] NULL,
	[Created] [datetime] NOT NULL,
	[Label] [varchar](64) NULL,
	[Username] [nvarchar](254) NOT NULL,
	[LastModified] [datetime] NOT NULL,
	CONSTRAINT [PK_StructureBase] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) as Node ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE graph.[Structure] ADD  CONSTRAINT [DF_StructureBase_Verified]  DEFAULT ((0)) FOR [Verified]
GO

ALTER TABLE graph.[Structure] ADD  CONSTRAINT [DF_StructureBase_Confidence]  DEFAULT ((0.5)) FOR [Confidence]
GO

ALTER TABLE graph.[Structure] ADD  CONSTRAINT [DF_Structure_Created]  DEFAULT (getutcdate()) FOR [Created]
GO

ALTER TABLE graph.[Structure] ADD  CONSTRAINT [DF_Structure_Username]  DEFAULT (N'') FOR [Username]
GO

ALTER TABLE graph.[Structure] ADD  CONSTRAINT [DF_Structure_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
GO
 
ALTER TABLE graph.[Structure]  WITH CHECK ADD  CONSTRAINT [FK_Graph_Structure_Structure] FOREIGN KEY([ParentID])
REFERENCES graph.[Structure] ([ID])
GO

ALTER TABLE graph.[Structure] CHECK CONSTRAINT [FK_Graph_Structure_Structure]
GO

ALTER TABLE graph.[Structure]  WITH CHECK ADD  CONSTRAINT [FK_Graph_StructureBase_StructureType] FOREIGN KEY([TypeID])
REFERENCES [dbo].[StructureType] ([ID])
GO

print '  Creating structure link edge table'
go

/*  Structure Link */
DROP TABLE IF EXISTS graph.[StructureLink]
GO
CREATE TABLE graph.[StructureLink](
	[Bidirectional] [bit] NOT NULL,
	[Tags] [xml] NULL,
	[Username] [nvarchar](254) NOT NULL,
	[Created] [datetime] NOT NULL,
	[LastModified] [datetime] NOT NULL,
	) as Edge ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


ALTER TABLE graph.[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Bidirectional]  DEFAULT ((0)) FOR [Bidirectional]
GO

ALTER TABLE graph.[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Username]  DEFAULT (N'') FOR [Username]
GO

ALTER TABLE graph.[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Created]  DEFAULT (getutcdate()) FOR [Created]
GO

ALTER TABLE graph.[StructureLink] ADD  CONSTRAINT [DF_StructureLink_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
GO

ALTER TABLE graph.[StructureLink] ADD CONSTRAINT EC_StructureLink1 CONNECTION (graph.Structure TO graph.Structure)

/*  Location */

print '  Creating annotation location node table'
go

DROP TABLE IF EXISTS graph.Location
GO

CREATE TABLE graph.Location (
	[ID] [bigint] IDENTITY(1,1) NOT NULL,
	[ParentID] [bigint] NOT NULL,
	[Z] [bigint] NOT NULL,
	[Closed] [bit] NOT NULL,
	[Version] [timestamp] NOT NULL,
	[Overlay] [varbinary](max) NULL,
	[Tags] [xml] NULL,
	[Terminal] [bit] NOT NULL,
	[OffEdge] [bit] NOT NULL,
	[TypeCode] [smallint] NOT NULL,
	[LastModified] [datetime] NOT NULL,
	[Created] [datetime] NOT NULL,
	[Username] [nvarchar](254) NOT NULL,
	[MosaicShape] [geometry] NOT NULL,
	[VolumeShape] [geometry] NOT NULL,
	[X]  AS (isnull([MosaicShape].[STCentroid]().STX,isnull([MosaicShape].[STX],(0)))) PERSISTED NOT NULL,
	[Y]  AS (isnull([MosaicShape].[STCentroid]().STY,isnull([MosaicShape].[STY],(0)))) PERSISTED NOT NULL,
	[VolumeX]  AS (isnull([VolumeShape].[STCentroid]().STX,isnull([VolumeShape].[STX],isnull([VolumeShape].[STEnvelope]().STCentroid().STX,(0))))) PERSISTED NOT NULL,
	[VolumeY]  AS (isnull([VolumeShape].[STCentroid]().STY,isnull([VolumeShape].[STY],isnull([VolumeShape].[STEnvelope]().STCentroid().STY,(0))))) PERSISTED NOT NULL,
	[Width] [float] NULL,
	[Radius]  AS (case [MosaicShape].[STDimension]() when (0) then (0) when (1) then [MosaicShape].[STLength]()/(2.0) when (2) then sqrt([MosaicShape].[STArea]()/pi())  end) PERSISTED NOT NULL,
	 CONSTRAINT [PK_Location] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)  ON [PRIMARY]
  ) as Node ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


ALTER TABLE [graph].[Location]  WITH CHECK ADD  CONSTRAINT [FK_Graph_Location_Structure] FOREIGN KEY([ParentID])
REFERENCES [graph].[Structure] ([ID])
ON DELETE CASCADE
GO

ALTER TABLE [graph].[Location] CHECK CONSTRAINT [FK_Graph_Location_Structure]
GO

/****** Object:  Index [VolumeShape_Index]    Script Date: 10/26/2020 1:07:19 PM ******/
CREATE SPATIAL INDEX [Graph_VolumeShape_Index] ON [graph].[Location]
(
	[VolumeShape]
)USING  GEOMETRY_AUTO_GRID 
WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/*  Location Link */ 

print '  Creating annotation location link edge table'
go

  Drop Table If Exists graph.LocationLink
  go

  CREATE TABLE graph.LocationLink (
    [Username] [nvarchar](254) NOT NULL,
	[Created] [datetime] NOT NULL,
	[Distance] [float] NOT NULL, --Distance between locations as measured by
	CONSTRAINT [PK_LocationLink] PRIMARY KEY CLUSTERED 
	(
		$edge_id ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
	 
 ) as EDGE ON [PRIMARY] 
 go

 ALTER TABLE graph.LocationLink ADD CONSTRAINT EC_LocationLink1 CONNECTION (graph.Location TO graph.Location)
  
 /********* Structure Attach Location */

 
print '  Creating Child<->Parent structure annotation location attachment table'
go

DROP TABLE IF Exists graph.StructureAttachLocation
GO

/* Create a table in our graph database with edges indicating roughly where parents and children attach*/
CREATE TABLE graph.StructureAttachLocation(
	 FromStructureID bigint NOT NULL,
	 ToStructureID   bigint NOT NULL,
	 Distance		 float
	CONSTRAINT [PK_StructureAttachLocation] PRIMARY KEY CLUSTERED 
	(
		$edge_id ASC
	) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 90, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) as EDGE ON [PRIMARY]

go 

ALTER TABLE graph.StructureAttachLocation ADD CONSTRAINT EC_StructureAttachLocation1 CONNECTION (graph.Location TO graph.Location)

ALTER TABLE [graph].[StructureAttachLocation]  WITH CHECK ADD  CONSTRAINT [FK_StructureAttachLocation_FromStructure1] FOREIGN KEY([FromStructureID])
REFERENCES [graph].[Structure] ([ID])
ON DELETE CASCADE
GO


ALTER TABLE [graph].[StructureAttachLocation]  WITH CHECK ADD  CONSTRAINT [FK_StructureAttachLocation_ToStructure1] FOREIGN KEY([ToStructureID])
REFERENCES [graph].[Structure] ([ID])
ON DELETE NO ACTION
GO


/************ Populate graph *************/

print 'Populating graph database'
go


  print ' Adding structure nodes'
  go

  SET IDENTITY_INSERT graph.Structure ON 
  GO

  insert into graph.Structure(ID, TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified)
  select ID, TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified from Structure S 
  GO

  
  SET IDENTITY_INSERT graph.Structure OFF 
  GO

  print ' Adding structure link edges'
  go
   
  /*Create structure link edges */
  insert into graph.StructureLink($from_id, $to_id, Bidirectional, Tags, Created, Username, LastModified)
  select SourceStruct.NodeID, TargetStruct.NodeID, Bidirectional, Tags, Created, Username, LastModified from StructureLink SL 
    INNER JOIN (Select $node_id as NodeID, ID from graph.Structure) SourceStruct on SourceStruct.ID = SL.SourceID
	INNER JOIN (Select $node_id as NodeID, ID from graph.Structure) TargetStruct on TargetStruct.ID = SL.TargetID
  GO

  /* Create edges going in reverse for bidirectional links*/
  insert into graph.StructureLink($to_id, $from_id, Bidirectional, Tags, Created, Username, LastModified)
  select SourceStruct.NodeID, TargetStruct.NodeID, Bidirectional, Tags, Created, Username, LastModified from StructureLink SL 
    INNER JOIN (Select $node_id as NodeID, ID from graph.Structure) SourceStruct on SourceStruct.ID = SL.SourceID
	INNER JOIN (Select $node_id as NodeID, ID from graph.Structure) TargetStruct on TargetStruct.ID = SL.TargetID
	where SL.Bidirectional = 1
  GO

  print ' Adding annotation location nodes'
  go
   
  SET IDENTITY_INSERT graph.Location ON 
  GO

  insert into graph.Location(ID, ParentID, Z, Closed, Overlay, Tags, Terminal, OffEdge, TypeCode, LastModified, Created, Username, MosaicShape, VolumeShape, Width)
  select ID, ParentID, Z, Closed, Overlay, Tags, Terminal, OffEdge, TypeCode, LastModified, Created, Username, MosaicShape, VolumeShape, Width from Location L 
  GO

  
  SET IDENTITY_INSERT graph.Location OFF 
  GO

  
  print ' Adding annotation location link edges'
  go

  insert into graph.LocationLink($from_id, $to_id, Created, Username, Distance)
  select LA.NodeID, LB.NodeID, Created, Username, [dbo].ufnDistance3D(LA.X, LA.Y, LA.Z, LB.X, LB.Y, LB.Z) from LocationLink LL 
    INNER JOIN (Select $node_id as NodeID, ID, X, Y, Z from graph.Location) LA on LL.A = LA.ID
	INNER JOIN (Select $node_id as NodeID, ID, X, Y, Z from graph.Location) LB on LL.B = LB.ID
  GO

  print 'Done!'
  go