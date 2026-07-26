/*Run this block of code if you change any of the first three queries or annotations for the structure.  It will clear the cache.*/
/* Requires: database must have a MEMORY_OPTIMIZED_FILEGROUP and In-Memory OLTP enabled. */
/* IMPORTANT: Run the entire script from the top so the type is recreated. If you get "truncated in column GlialShape",
   the DB still has the old type—run the block below once to drop and recreate it, then run the rest. */

drop type if exists dbo.ResultsType;
drop type if exists dbo.ChildDistanceType;
drop type if exists dbo.CandidatesType;

go

create type dbo.CandidatesType as table (
	GlialID int not null,
	SynapseLocID int not null,
	SynapseStructID int not null,
	XYDist float not null,
	ZDist float not null,
	primary key nonclustered (GlialID, SynapseLocID),
	index ix_SynapseStructID nonclustered (SynapseStructID)
) with (memory_optimized = on);

create type dbo.ChildDistanceType as table (
	SynapseStructID int not null,
	Dist float not null,
	primary key nonclustered (SynapseStructID)
) with (memory_optimized = on);

create type dbo.ResultsType as table (
	GlialID int not null,
	SynapseStructID int not null,
	Dist float not null,
	primary key nonclustered (GlialID, SynapseStructID)
) with (memory_optimized = on);

go

declare @SearchDistance float
set @SearchDistance = 500 /*Search within 500 nanometers*/
declare @ZRange float
set @ZRange = @SearchDistance / dbo.ZScale()

declare @GlialID int
set @GlialID = 8887

declare @SynapseTypeIDs dbo.integer_list

insert into @SynapseTypeIDs (ID) values (73), (34), (28);

declare @Candidates dbo.CandidatesType;

insert into @Candidates (GlialID, SynapseLocID, SynapseStructID, XYDist, ZDist)
select GlialLoc.ID as GlialID, SynapseLoc.ID as SynapseLocID,
	   SynapseLoc.ParentID as SynapseStructID,
	   GlialLoc.VolumeShape.STDistance(SynapseLoc.VolumeShape) * dbo.XYScale() as XYDist,
	   ABS(SynapseLoc.Z - GlialLoc.Z) * dbo.ZScale() as ZDist
from Location GlialLoc
inner join Location SynapseLoc ON SynapseLoc.Z BETWEEN GlialLoc.Z - @ZRange AND GlialLoc.Z + @ZRange
inner join Structure SynapseStruct ON SynapseLoc.ParentID = SynapseStruct.ID
inner join @SynapseTypeIDs SynapseTypes ON SynapseTypes.ID = SynapseStruct.TypeID
where GlialLoc.ParentID = @GlialID and GlialLoc.VolumeShape.STDistance(SynapseLoc.VolumeShape) * dbo.XYScale() < @SearchDistance;

/*Remove candidates that are too far in 3D space*/
delete from @Candidates
where SQRT(ZDist * ZDist + XYDist * XYDist) > @SearchDistance;

/* Join to Location when geometry columns are needed. */
select C.GlialID,
	   GlialLoc.VolumeShape.ToString() as GlialShape,
	   SynapseLoc.VolumeShape.ToString() as SynapseShape,
	   C.SynapseLocID, C.SynapseStructID, C.XYDist, C.ZDist
from @Candidates C
join Location GlialLoc on GlialLoc.ID = C.GlialID
join Location SynapseLoc on SynapseLoc.ID = C.SynapseLocID;

declare @ChildDistance dbo.ChildDistanceType;

insert into @ChildDistance (SynapseStructID, Dist)
select C.SynapseStructID, 
	   MIN(SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist)) as Dist
from @Candidates C
GROUP BY C.SynapseStructID

	   
/* Get the real number of synapses since 0 distance results show up multiple times in the next query*/
select COUNT(SynapseStructID) from @ChildDistance;

/* Select the closest pair of glial and synapse annotations, recording distance */
declare @Results dbo.ResultsType;

insert into @Results (GlialID, SynapseStructID, Dist)
select C.GlialID, 
	   C.SynapseStructID as SynapseStructID,
	   SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist) as Dist
from @Candidates C
inner join @ChildDistance CD ON CD.SynapseStructID = C.SynapseStructID
WHERE SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist) = CD.Dist
	   
/* @ShapeMap: regular table variable (geometry not supported in memory-optimized).
   PK on ID is prudent: ID is unique (one per synapse structure + one glial).
   Spatial index: not supported on table variables. For spatial indexing, use a
   #temp table, add a clustered primary key on ID, then CREATE SPATIAL INDEX
   with BOUNDING_BOX and GEOMETRY_AUTO_GRID. */
declare @ShapeMap table (ID int not null primary key, Shape geometry not null);

insert into @ShapeMap (ID, Shape)
select L.ParentID, geometry::UnionAggregate(L.VolumeShape)
from Location L
inner join @Results R ON R.SynapseStructID = L.ParentID
group by L.ParentID;

declare @center geometry;
set @center = (select geometry::UnionAggregate(VolumeShape) from Location where ParentID = @GlialID);

insert into @ShapeMap (ID, Shape) values (@GlialID, @center);

select ID, Shape, Shape.ToString() from @ShapeMap;

go

drop type if exists dbo.ResultsType;
drop type if exists dbo.ChildDistanceType;
drop type if exists dbo.CandidatesType;
