/*
 * PURPOSE
 * -------
 * For a single glial structure (@GlialID = 8887), find every synapse structure
 * of the specified types (TypeIDs 73, 34, 28) whose annotation locations fall
 * within @SearchDistance (500 nm) of any annotation location belonging to the
 * glial cell, in both XY and Z.  For each candidate synapse structure, keep only
 * the closest glial–synapse location pair (3-D Euclidean distance).
 *
 * FINAL OUTPUT
 * ------------
 * A single result set (@ShapeMap) containing one row per synapse structure that
 * passed the distance filter, plus one row for the glial structure itself.  Each
 * row carries the structure ID, the union of all its annotation geometries as a
 * geometry value, and the WKT string of that geometry — ready for spatial
 * visualisation or export.
 *
 * Intermediate result sets emitted during execution:
 *   1. Candidate locations with glial and synapse geometry strings, XY/Z distances.
 *   2. A count of unique synapse structures in the candidate set.
 */

/* -------------------------------------------------------------------------
   CandidatesType: holds every (glial location, synapse location) pair that
   passes the initial distance filter.  Primary key on (GlialID, SynapseLocID)
   prevents duplicates; secondary index on SynapseStructID speeds the later
   per-structure aggregation.
   ------------------------------------------------------------------------- */
create type dbo.CandidatesType as table (
	GlialLocID int not null,
	SynapseLocID int not null,
	SynapseStructID int not null,
	XYDist float not null,
	ZDist float not null,
	primary key nonclustered (GlialLocID, SynapseLocID),
	index ix_SynapseStructID nonclustered (SynapseStructID)
) with (memory_optimized = on);

/* -------------------------------------------------------------------------
   ChildDistanceType: one row per synapse structure recording the minimum
   3-D distance from any of its locations to any glial location.  Used to
   deduplicate candidates down to the single closest pair per structure.
   ------------------------------------------------------------------------- */
create type dbo.ChildDistanceType as table (
	SynapseStructID int not null,
	Dist float not null,
	primary key nonclustered (SynapseStructID)
) with (memory_optimized = on);

/* -------------------------------------------------------------------------
   ResultsType: final filtered set — one row per (glial, synapse structure)
   pair, keeping only the closest location pair for each synapse structure.
   ------------------------------------------------------------------------- */
create type dbo.ResultsType as table (
	GlialID int not null,
	SynapseStructID int not null,
	SynapseLocID int not null,
	Dist float not null,
	primary key nonclustered (GlialID, SynapseStructID),
	index SynapseLocID nonclustered (SynapseLocID)
) with (memory_optimized = on);

go

/* -------------------------------------------------------------------------
   Parameters: search radius in nanometers and the corresponding Z-slice
   range in raw Z units (converted via dbo.ZScale()).
   ------------------------------------------------------------------------- */
declare @SearchDistance float
set @SearchDistance = 500 /*Search within 500 nanometers*/
declare @ZRange float
set @ZRange = @SearchDistance / dbo.ZScale()

/* The glial structure whose neighbourhood is being searched. */
declare @GlialID int
set @GlialID = 9025

/* The synapse TypeIDs to include in the search. */
declare @SynapseTypeIDs dbo.integer_list
insert into @SynapseTypeIDs (ID) values (73), (34), (28);

/* -------------------------------------------------------------------------
   Build the candidate set: every (glial location, synapse location) pair
   where the synapse location is within @ZRange slices of the glial location
   AND within @SearchDistance nm in XY.  Both XY and Z distances are stored
   in physical units (nm) for the distance calculations that follow.
   ------------------------------------------------------------------------- */
declare @Candidates dbo.CandidatesType;

insert into @Candidates (GlialLocID, SynapseLocID, SynapseStructID, XYDist, ZDist)
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

/* -------------------------------------------------------------------------
   RESULT SET 1: inspect candidate pairs with their geometry strings and
   distances.  Joins back to Location to retrieve the geometry columns,
   which are not stored in the memory-optimized @Candidates table.
   ------------------------------------------------------------------------- */
select C.GlialLocID, C.SynapseLocID, C.SynapseStructID, C.XYDist, C.ZDist,
	   GlialLoc.VolumeShape.ToString() as GlialShape,
	   SynapseLoc.VolumeShape.ToString() as SynapseShape
from @Candidates C
join Location GlialLoc on GlialLoc.ID = C.GlialLocID
join Location SynapseLoc on SynapseLoc.ID = C.SynapseLocID;

/* -------------------------------------------------------------------------
   For each synapse structure, compute the minimum 3-D Euclidean distance
   across all of its candidate location pairs.  This is the threshold used
   below to select the single closest pair per structure.
   ------------------------------------------------------------------------- */
declare @ChildDistance dbo.ChildDistanceType;

insert into @ChildDistance (SynapseStructID, Dist)
select C.SynapseStructID, 
	   MIN(SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist)) as Dist
from @Candidates C
GROUP BY C.SynapseStructID
	   
/* -------------------------------------------------------------------------
   RESULT SET 2: the number of unique synapse structures within range.
   Counted from @ChildDistance rather than @Candidates because a structure
   at distance 0 (centroid coincides with glial geometry) can appear in
   @Candidates multiple times and would be overcounted there.
   ------------------------------------------------------------------------- */
select COUNT(SynapseStructID) from @ChildDistance;


/* -------------------------------------------------------------------------
   Reduce @Candidates to one row per synapse structure: keep only the pair
   whose 3-D distance equals the per-structure minimum computed above.
   ------------------------------------------------------------------------- */
declare @Results dbo.ResultsType;

insert into @Results (GlialID, SynapseStructID,  SynapseLocID, Dist)
select C.GlialLocID,
	   C.SynapseStructID as SynapseStructID,
	   C.SynapseLocID,
	   SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist) as Dist
from @Candidates C
inner join @ChildDistance CD ON CD.SynapseStructID = C.SynapseStructID
WHERE SQRT(C.ZDist * C.ZDist + C.XYDist * C.XYDist) = CD.Dist

/* -------------------------------------------------------------------------
   RESULT SET 3: Candidate locations closest approach to the target cell
   ------------------------------------------------------------------------- */
select *
from @Results R 


/* -------------------------------------------------------------------------
   @ShapeMap: a regular (non-memory-optimized) table variable used because
   geometry columns are not supported in memory-optimized types.
   PK on ID is intentional — each synapse structure and the glial cell each
   contribute exactly one row.
   Note: spatial indexes are not supported on table variables.  If spatial
   indexing is needed, replace this with a #temp table, add a clustered PK,
   then CREATE SPATIAL INDEX with BOUNDING_BOX and GEOMETRY_AUTO_GRID.
   ------------------------------------------------------------------------- */
declare @ShapeMap table (ID int not null primary key, Shape geometry not null);

/* For each synapse structure in the results, union all its annotation
   location geometries into a single shape representing the whole structure. */
insert into @ShapeMap (ID, Shape)
select L.ParentID, geometry::UnionAggregate(L.VolumeShape)
from Location L
inner join @Results R ON R.SynapseStructID = L.ParentID
group by L.ParentID;

/* Compute the union of all glial annotation geometries and add it to the
   shape map so the glial cell is included in the final output alongside
   the nearby synapse structures. */
declare @center geometry;
set @center = (select geometry::UnionAggregate(VolumeShape) from Location where ParentID = @GlialID);

insert into @ShapeMap (ID, Shape) values (@GlialID, @center);

/* -------------------------------------------------------------------------
   RESULT SET 4 (primary output): one row per structure — the glial cell
   plus every synapse structure within range — with its structure ID, the
   unioned geometry object, and the WKT string of that geometry.
   ------------------------------------------------------------------------- */
select ID, Shape, Shape.ToString() from @ShapeMap;

go

/* -------------------------------------------------------------------------
   Cleanup: drop the three working types so they do not persist in the
   database after the script completes.
   ------------------------------------------------------------------------- */
drop type if exists dbo.ResultsType;
drop type if exists dbo.ChildDistanceType;
drop type if exists dbo.CandidatesType;