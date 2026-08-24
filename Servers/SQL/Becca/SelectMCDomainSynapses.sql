/*
 * PURPOSE
 * -------
 * Same analysis as MCDomain_synapses_2.sql, packaged as a reusable procedure
 * that processes one glial structure per call.
 *
 * For @GlialID, find every synapse structure of the types in @SynapseTypeIDs
 * whose annotation locations fall within @SearchDistance nm of any glial
 * annotation location (XY and Z).  Keep the closest glial–synapse location
 * pair per synapse structure, then emit four result sets:
 *
 *   1. Candidates     — every passing (glial loc, synapse loc) pair
 *   2. SynapseCount   — count of unique synapse structures in range
 *   3. ClosestPairs   — one closest pair per synapse structure
 *   4. ShapeMap       — unioned geometry for the glial cell and each synapse
 *
 * Intermediate table variables are scoped to a single execution.  Do not try
 * to reuse them across glial IDs: each neighbourhood is a different spatial
 * join, and leftover rows would mix cells.  Invoke this procedure once per
 * glial ID (see Invoke-MCDomainSynapses.ps1).
 *
 * The batch runner always consumes four result sets.  Even when @GlialID has
 * no locations, this procedure must still emit four (empty) sets.
 *
 * Requires: MEMORY_OPTIMIZED_FILEGROUP / In-Memory OLTP, dbo.integer_list,
 *           dbo.XYScale(), dbo.ZScale().
 */

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.SelectMCDomainSynapses', N'P') IS NOT NULL
    DROP PROCEDURE dbo.SelectMCDomainSynapses;
GO

DROP TYPE IF EXISTS dbo.MCDomainResultsType;
DROP TYPE IF EXISTS dbo.MCDomainChildDistanceType;
DROP TYPE IF EXISTS dbo.MCDomainCandidatesType;
GO

/* -------------------------------------------------------------------------
   CandidatesType: every (glial location, synapse location) pair that passes
   the 3-D distance filter.  PK on (GlialLocID, SynapseLocID) prevents
   duplicates.  Dist is stored so later steps do not recompute SQRT.
   Geometry cannot live on a memory-optimized type — result set 1 joins
   Location to recover WKT.
   ------------------------------------------------------------------------- */
CREATE TYPE dbo.MCDomainCandidatesType AS TABLE (
    GlialLocID int NOT NULL,
    SynapseLocID int NOT NULL,
    SynapseStructID int NOT NULL,
    XYDist float NOT NULL,
    ZDist float NOT NULL,
    Dist float NOT NULL,
    PRIMARY KEY NONCLUSTERED (GlialLocID, SynapseLocID),
    INDEX ix_SynapseStructID NONCLUSTERED (SynapseStructID)
) WITH (MEMORY_OPTIMIZED = ON);

/* -------------------------------------------------------------------------
   ChildDistanceType: one row per synapse structure with its minimum 3-D
   distance.  Used only for the unique-structure count — a structure at
   distance 0 can appear many times in @Candidates and would be overcounted
   there.
   ------------------------------------------------------------------------- */
CREATE TYPE dbo.MCDomainChildDistanceType AS TABLE (
    SynapseStructID int NOT NULL,
    Dist float NOT NULL,
    PRIMARY KEY NONCLUSTERED (SynapseStructID)
) WITH (MEMORY_OPTIMIZED = ON);

/* -------------------------------------------------------------------------
   ResultsType: one closest location pair per synapse structure.
   Column GlialID is the glial *location* ID (same name as MCDomain_synapses_2)
   so existing CSV consumers keep working.
   ------------------------------------------------------------------------- */
CREATE TYPE dbo.MCDomainResultsType AS TABLE (
    GlialID int NOT NULL,
    SynapseStructID int NOT NULL,
    SynapseLocID int NOT NULL,
    Dist float NOT NULL,
    PRIMARY KEY NONCLUSTERED (GlialID, SynapseStructID),
    INDEX SynapseLocID NONCLUSTERED (SynapseLocID)
) WITH (MEMORY_OPTIMIZED = ON);
GO

CREATE PROCEDURE dbo.SelectMCDomainSynapses
    @GlialID int,
    @SearchDistance float = 500,
    @SynapseTypeIDs dbo.integer_list READONLY
AS
BEGIN
    SET NOCOUNT ON;

    /* Scalar UDFs are captured once.  Calling XYScale()/ZScale() in the
       per-pair SELECT/WHERE (as the original script did) blocks parallelism
       and repeats the same constant for every candidate. */
    DECLARE @XYScale float = dbo.XYScale();
    DECLARE @ZScale float = dbo.ZScale();
    DECLARE @ZRange float = @SearchDistance / @ZScale;
    DECLARE @XYRadiusUnits float = @SearchDistance / @XYScale;
    DECLARE @SearchDistanceSq float = @SearchDistance * @SearchDistance;

    DECLARE @Types dbo.integer_list;
    INSERT INTO @Types (ID)
    SELECT ID FROM @SynapseTypeIDs;

    IF NOT EXISTS (SELECT 1 FROM @Types)
        INSERT INTO @Types (ID) VALUES (73), (34), (28);

    DECLARE @GlialMinZ bigint;
    DECLARE @GlialMaxZ bigint;

    SELECT @GlialMinZ = MIN(Z), @GlialMaxZ = MAX(Z)
    FROM Location
    WHERE ParentID = @GlialID;

    /* Missing glial (or a structure with no locations): still return the
       four empty result sets the PowerShell runner expects. */
    IF @GlialMinZ IS NULL
    BEGIN
        SELECT CAST(NULL AS int) AS GlialLocID,
               CAST(NULL AS int) AS SynapseLocID,
               CAST(NULL AS int) AS SynapseStructID,
               CAST(NULL AS float) AS XYDist,
               CAST(NULL AS float) AS ZDist,
               CAST(NULL AS nvarchar(max)) AS GlialShape,
               CAST(NULL AS nvarchar(max)) AS SynapseShape
        WHERE 1 = 0;

        SELECT CAST(0 AS int) AS SynapseCount;

        SELECT CAST(NULL AS int) AS GlialID,
               CAST(NULL AS int) AS SynapseStructID,
               CAST(NULL AS int) AS SynapseLocID,
               CAST(NULL AS float) AS Dist
        WHERE 1 = 0;

        SELECT CAST(NULL AS int) AS ID,
               CAST(NULL AS nvarchar(max)) AS ShapeWKT
        WHERE 1 = 0;

        RETURN;
    END

    IF OBJECT_ID(N'tempdb..#GlialLoc') IS NOT NULL
        DROP TABLE #GlialLoc;
    IF OBJECT_ID(N'tempdb..#SynapseLoc') IS NOT NULL
        DROP TABLE #SynapseLoc;

    /* SearchBox is the axis-aligned envelope expanded by the XY search
       radius in database units.  STIntersects(SearchBox, synapse) is a
       conservative prefilter: any pair with true STDistance < radius must
       intersect this box, but the box can also admit pairs that later fail
       the exact 3-D test.  Geometry is not allowed on memory-optimized
       types, so these stay as #temp tables. */
    CREATE TABLE #GlialLoc (
        ID bigint NOT NULL PRIMARY KEY,
        Z bigint NOT NULL,
        VolumeShape geometry NOT NULL,
        SearchBox geometry NOT NULL
    );

    INSERT INTO #GlialLoc (ID, Z, VolumeShape, SearchBox)
    SELECT L.ID,
           L.Z,
           L.VolumeShape,
           L.VolumeShape.STEnvelope().STBuffer(@XYRadiusUnits)
    FROM Location L
    WHERE L.ParentID = @GlialID;

    /* Type-filter first, then restrict to the glial cell's full Z span
       plus one search window.  The original join started from all Location
       rows on nearby sections (cell traces included) and only then checked
       Structure.TypeID.  Per-pair Z is applied again below — this span is
       only a coarse cull so #SynapseLoc can cover every glial location. */
    CREATE TABLE #SynapseLoc (
        ID bigint NOT NULL PRIMARY KEY,
        ParentID bigint NOT NULL,
        Z bigint NOT NULL,
        VolumeShape geometry NOT NULL
    );

    INSERT INTO #SynapseLoc (ID, ParentID, Z, VolumeShape)
    SELECT L.ID, L.ParentID, L.Z, L.VolumeShape
    FROM Location L
    INNER JOIN Structure S ON S.ID = L.ParentID
    INNER JOIN @Types T ON T.ID = S.TypeID
    WHERE L.Z BETWEEN @GlialMinZ - @ZRange AND @GlialMaxZ + @ZRange;

    CREATE NONCLUSTERED INDEX IX_SynapseLoc_Z ON #SynapseLoc (Z);

    /* Same predicates as MCDomain_synapses_2:
         1. synapse Z within @ZRange slices of that glial location
         2. XY STDistance * XYScale < @SearchDistance
         3. 3-D Euclidean distance <= @SearchDistance
       STDistance is evaluated once (the original computed it in SELECT and
       again in WHERE).  The 3-D cutoff is applied here instead of a later
       DELETE so rejected pairs never enter @Candidates. */
    DECLARE @Candidates dbo.MCDomainCandidatesType;

    INSERT INTO @Candidates (GlialLocID, SynapseLocID, SynapseStructID, XYDist, ZDist, Dist)
    SELECT G.ID,
           S.ID,
           S.ParentID,
           D.XYDist,
           D.ZDist,
           SQRT(D.XYDist * D.XYDist + D.ZDist * D.ZDist)
    FROM #GlialLoc G
    INNER JOIN #SynapseLoc S
        ON S.Z BETWEEN G.Z - @ZRange AND G.Z + @ZRange
       AND G.SearchBox.STIntersects(S.VolumeShape) = 1
    CROSS APPLY (
        SELECT G.VolumeShape.STDistance(S.VolumeShape) * @XYScale AS XYDist,
               ABS(CONVERT(float, S.Z - G.Z)) * @ZScale AS ZDist
    ) D
    WHERE D.XYDist < @SearchDistance
      AND D.XYDist * D.XYDist + D.ZDist * D.ZDist <= @SearchDistanceSq;

    /* RESULT SET 1: candidate pairs with WKT.  Geometry is re-read from
       Location because it cannot be stored on the memory-optimized type. */
    SELECT C.GlialLocID, C.SynapseLocID, C.SynapseStructID, C.XYDist, C.ZDist,
           GlialLoc.VolumeShape.ToString() AS GlialShape,
           SynapseLoc.VolumeShape.ToString() AS SynapseShape
    FROM @Candidates C
    JOIN Location GlialLoc ON GlialLoc.ID = C.GlialLocID
    JOIN Location SynapseLoc ON SynapseLoc.ID = C.SynapseLocID;

    DECLARE @ChildDistance dbo.MCDomainChildDistanceType;

    INSERT INTO @ChildDistance (SynapseStructID, Dist)
    SELECT C.SynapseStructID, MIN(C.Dist)
    FROM @Candidates C
    GROUP BY C.SynapseStructID;

    /* RESULT SET 2: unique synapse structures, not candidate-pair rows. */
    SELECT COUNT(SynapseStructID) AS SynapseCount
    FROM @ChildDistance;

    /* One row per synapse structure.  The original kept every pair whose
       distance equalled the per-structure minimum; two synapse locations
       at the same distance from the same glial location violated the
       (GlialLocID, SynapseStructID) primary key (seen on glial 5048).
       ROW_NUMBER breaks ties by location ID. */
    DECLARE @Results dbo.MCDomainResultsType;

    INSERT INTO @Results (GlialID, SynapseStructID, SynapseLocID, Dist)
    SELECT GlialLocID, SynapseStructID, SynapseLocID, Dist
    FROM (
        SELECT C.GlialLocID,
               C.SynapseStructID,
               C.SynapseLocID,
               C.Dist,
               ROW_NUMBER() OVER (
                   PARTITION BY C.SynapseStructID
                   ORDER BY C.Dist, C.GlialLocID, C.SynapseLocID
               ) AS rn
        FROM @Candidates C
    ) ranked
    WHERE rn = 1;

    /* RESULT SET 3: closest approach per synapse structure.
       GlialID here is GlialLocID — see ResultsType comment. */
    SELECT *
    FROM @Results R;

    /* @ShapeMap cannot be memory-optimized (geometry).  PK on ID is
       intentional: one row per synapse structure plus one for the glial
       cell.  Spatial indexes are not supported on table variables. */
    DECLARE @ShapeMap TABLE (
        ID int NOT NULL PRIMARY KEY,
        Shape geometry NOT NULL
    );

    INSERT INTO @ShapeMap (ID, Shape)
    SELECT L.ParentID, geometry::UnionAggregate(L.VolumeShape)
    FROM Location L
    INNER JOIN @Results R ON R.SynapseStructID = L.ParentID
    GROUP BY L.ParentID;

    DECLARE @center geometry = (
        SELECT geometry::UnionAggregate(VolumeShape)
        FROM Location
        WHERE ParentID = @GlialID
    );

    /* UnionAggregate of an empty set is NULL; Shape is NOT NULL. */
    IF @center IS NOT NULL
        INSERT INTO @ShapeMap (ID, Shape) VALUES (@GlialID, @center);

    /* RESULT SET 4: WKT only.  Returning the geometry column requires
       Microsoft.SqlServer.Types on the client, which the batch runner
       does not have. */
    SELECT ID, Shape.ToString() AS ShapeWKT
    FROM @ShapeMap;
END
GO
