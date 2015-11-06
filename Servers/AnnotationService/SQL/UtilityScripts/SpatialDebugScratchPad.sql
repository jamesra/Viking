declare @Z bigint
declare @bbox geometry
declare @MinRadius float
declare @QueryDate datetime
declare @MinX float
declare @MinY float
declare @MaxX float
declare @MaxY float

set @Z = 250
set @MinRadius = 128
set @QueryDate = NULL

set @MinX = 32000
set @MinY = 32000
set @MaxX = 64000
set @MaxY = 64000

set @bbox = geometry::STGeomFromText('POLYGON(  ( '+ STR(@MinX,16,2) + ' ' + STR(@MinY,16,2) + ',' +
														   + STR(@MaxX,16,2) + ' ' + STR(@MinY,16,2) + ',' +
														   + STR(@MaxX,16,2) + ' ' + STR(@MaxY,16,2) + ',' +
														   + STR(@MinX,16,2) + ' ' + STR(@MaxY,16,2) + ',' +
														   + STR(@MinX,16,2) + ' ' + STR(@MinY,16,2) + ' ))', 0);

IF OBJECT_ID('tempdb..#LocationsInBounds') IS NOT NULL DROP TABLE #LocationsInBounds

--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

select * into #LocationsInBounds FROM Location where Z = @Z AND (VolumeShape.STIntersects(@bbox) = 1) AND Radius >= @MinRadius order by ID
	 
IF @QueryDate IS NOT NULL
	Select Loc.* from Location Loc JOIN #LocationsInBounds sl ON (sl.ID = Loc.ID) WHERE Loc.LastModified >= @QueryDate
ELSE
	Select Loc.* from Location Loc JOIN #LocationsInBounds sl ON (sl.ID = Loc.ID)
	 
IF @QueryDate IS NOT NULL
	-- Insert statements for procedure here
	Select ll.* from LocationLink ll JOIN #LocationsInBounds sl ON (sl.ID = ll.A) WHERE ll.Created >= @QueryDate 
	UNION
	Select ll.* from LocationLink ll JOIN #LocationsInBounds sl ON (sl.ID = ll.B) WHERE ll.Created >= @QueryDate
ELSE
	-- Insert statements for procedure here
	Select ll.* from LocationLink ll JOIN #LocationsInBounds sl ON (sl.ID = ll.A)
	UNION
	Select ll.* from LocationLink ll JOIN #LocationsInBounds sl ON (sl.ID = ll.B)