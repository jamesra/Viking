-- Minimal annotation schema for gRPC integration / CRUD smoke tests.
-- Not a substitute for the full AnnotationDatabase SSDT publish.
-- Idempotent: safe to re-run against AnnotationTest.

IF DB_ID(N'AnnotationTest') IS NULL
BEGIN
    CREATE DATABASE [AnnotationTest];
END
GO

USE [AnnotationTest];
GO

-- Required for persisted computed columns / geometry expressions.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.StructureType', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StructureType] (
        [ID]            BIGINT IDENTITY (1, 1) NOT NULL,
        [ParentID]      BIGINT NULL,
        [Name]          NCHAR (128) NOT NULL,
        [Notes]         NVARCHAR (MAX) NULL,
        [MarkupType]    NCHAR (16) CONSTRAINT [DF_StructureType_MarkupType] DEFAULT (N'Point') NOT NULL,
        [Tags]          XML NULL,
        [StructureTags] XML NULL,
        [Abstract]      BIT CONSTRAINT [DF_StructureType_Abstract] DEFAULT ((0)) NOT NULL,
        [Color]         INT CONSTRAINT [DF_StructureType_Color] DEFAULT (0xFFFFFF) NOT NULL,
        [Version]       ROWVERSION NOT NULL,
        [Code]          NCHAR (16) CONSTRAINT [DF_StructureType_Code] DEFAULT (N'No Code') NOT NULL,
        [HotKey]        CHAR (1) CONSTRAINT [DF_StructureType_HotKey] DEFAULT (CHAR(0)) NOT NULL,
        [Username]      NVARCHAR (254) CONSTRAINT [DF_StructureType_Username] DEFAULT (N'') NOT NULL,
        [LastModified]  DATETIME CONSTRAINT [DF_StructureType_LastModified] DEFAULT (getutcdate()) NOT NULL,
        [Created]       DATETIME CONSTRAINT [DF_StructureType_Created] DEFAULT (getutcdate()) NOT NULL,
        CONSTRAINT [PK_StructureType] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [FK_StructureType_StructureType] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[StructureType] ([ID])
    );
END
GO

IF OBJECT_ID(N'dbo.Structure', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Structure] (
        [ID]           BIGINT IDENTITY (1, 1) NOT NULL,
        [TypeID]       BIGINT NOT NULL,
        [Notes]        NVARCHAR (MAX) NULL,
        [Verified]     BIT CONSTRAINT [DF_StructureBase_Verified] DEFAULT ((0)) NOT NULL,
        [Tags]         XML NULL,
        [Confidence]   FLOAT (53) CONSTRAINT [DF_StructureBase_Confidence] DEFAULT ((0.5)) NOT NULL,
        [Version]      ROWVERSION NOT NULL,
        [ParentID]     BIGINT NULL,
        [Created]      DATETIME CONSTRAINT [DF_Structure_Created] DEFAULT (getutcdate()) NOT NULL,
        [Label]        VARCHAR (64) NULL,
        [Username]     NVARCHAR (254) CONSTRAINT [DF_Structure_Username] DEFAULT (N'') NOT NULL,
        [LastModified] DATETIME CONSTRAINT [DF_Structure_LastModified] DEFAULT (getutcdate()) NOT NULL,
        CONSTRAINT [PK_StructureBase] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [FK_Structure_Structure] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[Structure] ([ID]),
        CONSTRAINT [FK_StructureBase_StructureType] FOREIGN KEY ([TypeID]) REFERENCES [dbo].[StructureType] ([ID])
    );
END
GO

IF OBJECT_ID(N'dbo.DeletedLocations', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DeletedLocations] (
        [ID]        BIGINT NOT NULL,
        [DeletedOn] DATETIME CONSTRAINT [DF_DeletedLocations_DeletedOn] DEFAULT (getutcdate()) NOT NULL,
        CONSTRAINT [PK_DeletedLocations] PRIMARY KEY CLUSTERED ([ID] ASC)
    );
END
GO

IF OBJECT_ID(N'dbo.Location', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Location] (
        [ID]           BIGINT IDENTITY (1, 1) NOT NULL,
        [ParentID]     BIGINT NOT NULL,
        [Z]            BIGINT NOT NULL,
        [Closed]       BIT CONSTRAINT [DF_Location_Closed] DEFAULT ((0)) NOT NULL,
        [Version]      ROWVERSION NOT NULL,
        [Overlay]      VARBINARY (MAX) NULL,
        [Tags]         XML NULL,
        [Terminal]     BIT CONSTRAINT [DF_Location_Flagged] DEFAULT ((0)) NOT NULL,
        [OffEdge]      BIT CONSTRAINT [DF_Location_OffEdge] DEFAULT ((0)) NOT NULL,
        [TypeCode]     SMALLINT CONSTRAINT [DF_Location_TypeCode] DEFAULT ((1)) NOT NULL,
        [LastModified] DATETIME CONSTRAINT [DF_Location_LastModified] DEFAULT (getutcdate()) NOT NULL,
        [Created]      DATETIME CONSTRAINT [DF_Location_Created] DEFAULT (getutcdate()) NOT NULL,
        [Username]     NVARCHAR (254) CONSTRAINT [DF_Location_Username] DEFAULT (N'') NOT NULL,
        [MosaicShape]  [sys].[geometry] NOT NULL,
        [VolumeShape]  [sys].[geometry] NOT NULL,
        [X]            AS (isnull([MosaicShape].[STCentroid]().STX, isnull([MosaicShape].[STX], (0)))) PERSISTED NOT NULL,
        [Y]            AS (isnull([MosaicShape].[STCentroid]().STY, isnull([MosaicShape].[STY], (0)))) PERSISTED NOT NULL,
        [VolumeX]      AS (isnull([VolumeShape].[STCentroid]().STX, isnull([VolumeShape].[STX], isnull([VolumeShape].[STEnvelope]().STCentroid().STX, (0))))) PERSISTED NOT NULL,
        [VolumeY]      AS (isnull([VolumeShape].[STCentroid]().STY, isnull([VolumeShape].[STY], isnull([VolumeShape].[STEnvelope]().STCentroid().STY, (0))))) PERSISTED NOT NULL,
        [Width]        FLOAT (53) NULL,
        [Radius]       AS (case [MosaicShape].[STDimension]() when (0) then (0) when (1) then [MosaicShape].[STLength]() / (2.0) when (2) then sqrt([MosaicShape].[STArea]() / pi()) end) PERSISTED NOT NULL,
        CONSTRAINT [PK_Location] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [FK_Location_StructureBase1] FOREIGN KEY ([ParentID]) REFERENCES [dbo].[Structure] ([ID]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.LocationLink', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LocationLink] (
        [A]        BIGINT NOT NULL,
        [B]        BIGINT NOT NULL,
        [Username] NVARCHAR (254) CONSTRAINT [DF_LocationLink_Username] DEFAULT (N'') NOT NULL,
        [Created]  DATETIME CONSTRAINT [DF_LocationLink_Created] DEFAULT (getutcdate()) NOT NULL,
        CONSTRAINT [PK_LocationLink] PRIMARY KEY CLUSTERED ([A] ASC, [B] ASC),
        CONSTRAINT [chk_LocationLink_Self] CHECK ([A] <> [B]),
        CONSTRAINT [FK_LocationLink_Location] FOREIGN KEY ([A]) REFERENCES [dbo].[Location] ([ID]),
        CONSTRAINT [FK_LocationLink_Location1] FOREIGN KEY ([B]) REFERENCES [dbo].[Location] ([ID])
    );
END
GO

IF OBJECT_ID(N'dbo.StructureLink', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StructureLink] (
        [SourceID]      BIGINT NOT NULL,
        [TargetID]      BIGINT NOT NULL,
        [Bidirectional] BIT CONSTRAINT [DF_StructureLink_Bidirectional] DEFAULT ((0)) NOT NULL,
        [Tags]          XML NULL,
        [Username]      NVARCHAR (254) CONSTRAINT [DF_StructureLink_Username] DEFAULT (N'') NOT NULL,
        [Created]       DATETIME CONSTRAINT [DF_StructureLink_Created] DEFAULT (getutcdate()) NOT NULL,
        [LastModified]  DATETIME CONSTRAINT [DF_StructureLink_LastModified] DEFAULT (getutcdate()) NOT NULL,
        CONSTRAINT [chk_StructureLink_Self] CHECK ([SourceID] <> [TargetID]),
        CONSTRAINT [FK_StructureLinkSource_StructureBaseID] FOREIGN KEY ([SourceID]) REFERENCES [dbo].[Structure] ([ID]),
        CONSTRAINT [FK_StructureLinkTarget_StructureBaseID] FOREIGN KEY ([TargetID]) REFERENCES [dbo].[Structure] ([ID]),
        CONSTRAINT [source_target_unique] UNIQUE NONCLUSTERED ([SourceID] ASC, [TargetID] ASC)
    );
END
GO

IF OBJECT_ID(N'dbo.PermittedStructureLink', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PermittedStructureLink] (
        [SourceTypeID]  BIGINT NOT NULL,
        [TargetTypeID]  BIGINT NOT NULL,
        [Bidirectional] BIT NOT NULL,
        CONSTRAINT [PK_PermittedStructureLink] PRIMARY KEY CLUSTERED ([SourceTypeID] ASC, [TargetTypeID] ASC),
        CONSTRAINT [FK_PermittedStructureLink_SourceType] FOREIGN KEY ([SourceTypeID]) REFERENCES [dbo].[StructureType] ([ID]),
        CONSTRAINT [FK_PermittedStructureLink_TargetType] FOREIGN KEY ([TargetTypeID]) REFERENCES [dbo].[StructureType] ([ID])
    );
END
GO

-- Simplified DeepDeleteStructure (full SSDT version also clears children/links).
-- Required by StructureService.Update delete path.
IF OBJECT_ID(N'dbo.DeepDeleteStructure', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.DeepDeleteStructure
        @DeleteID bigint
    AS
    BEGIN
        SET NOCOUNT ON;

        IF OBJECT_ID(''tempdb..#StructuresToDelete'') IS NOT NULL
            DROP TABLE #StructuresToDelete;

        SELECT ID INTO #StructuresToDelete
        FROM dbo.Structure
        WHERE ID = @DeleteID OR ParentID = @DeleteID;

        DELETE FROM dbo.LocationLink
        WHERE A IN (SELECT ID FROM dbo.Location WHERE ParentID IN (SELECT ID FROM #StructuresToDelete))
           OR B IN (SELECT ID FROM dbo.Location WHERE ParentID IN (SELECT ID FROM #StructuresToDelete));

        DELETE FROM dbo.Location
        WHERE ParentID IN (SELECT ID FROM #StructuresToDelete);

        DELETE FROM dbo.StructureLink
        WHERE SourceID IN (SELECT ID FROM #StructuresToDelete)
           OR TargetID IN (SELECT ID FROM #StructuresToDelete);

        DELETE FROM dbo.Structure WHERE ParentID = @DeleteID;
        DELETE FROM dbo.Structure WHERE ID = @DeleteID;

        IF OBJECT_ID(''tempdb..#StructuresToDelete'') IS NOT NULL
            DROP TABLE #StructuresToDelete;
    END');
END
GO

-- MergeStructures (subset of full SSDT proc; enough for gRPC Merge RPC tests).
IF OBJECT_ID(N'dbo.MergeStructures', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.MergeStructures
        @KeepStructureID bigint,
        @MergeStructureID bigint
    AS
    BEGIN
        SET NOCOUNT ON;

        DECLARE @MergeNotes nvarchar(max) =
            (SELECT Notes FROM dbo.Structure WHERE ID = @MergeStructureID);

        UPDATE dbo.Location
        SET ParentID = @KeepStructureID
        WHERE ParentID = @MergeStructureID;

        UPDATE dbo.Structure
        SET ParentID = @KeepStructureID
        WHERE ParentID = @MergeStructureID;

        IF NOT (@MergeNotes IS NULL OR @MergeNotes = N'''')
        BEGIN
            DECLARE @crlf nvarchar(2) = CHAR(13) + CHAR(10);
            DECLARE @MergeHeader nvarchar(80) =
                N''*****BEGIN MERGE FROM '' + CONVERT(nvarchar(80), @MergeStructureID) + N''*****'';
            DECLARE @MergeFooter nvarchar(80) =
                N''*****END MERGE FROM '' + CONVERT(nvarchar(80), @MergeStructureID) + N''*****'';

            UPDATE dbo.Structure
            SET Notes = ISNULL(Notes, N'''') + @crlf + @MergeHeader + @crlf + @MergeNotes + @crlf + @MergeFooter + @crlf
            WHERE ID = @KeepStructureID;
        END

        DELETE FROM dbo.StructureLink
        WHERE (SourceID = @KeepStructureID AND TargetID = @MergeStructureID)
           OR (TargetID = @KeepStructureID AND SourceID = @MergeStructureID);

        UPDATE dbo.StructureLink SET TargetID = @KeepStructureID WHERE TargetID = @MergeStructureID;
        UPDATE dbo.StructureLink SET SourceID = @KeepStructureID WHERE SourceID = @MergeStructureID;

        DELETE FROM dbo.Structure WHERE ID = @MergeStructureID;
    END');
END
GO

-- Unfinished branch queries used by StructureService.
IF OBJECT_ID(N'dbo.SelectUnfinishedStructureBranches', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.SelectUnfinishedStructureBranches
        @StructureID bigint
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT ID FROM
            (SELECT LocationID, COUNT(LocationID) AS NumLinks FROM
                (
                    SELECT A AS LocationID FROM dbo.LocationLink
                    WHERE A IN (SELECT L.ID FROM dbo.Location L WHERE L.ParentID = @StructureID)
                    UNION ALL
                    SELECT B AS LocationID FROM dbo.LocationLink
                    WHERE B IN (SELECT L.ID FROM dbo.Location L WHERE L.ParentID = @StructureID)
                ) AS LinkedIDs
                GROUP BY LocationID) AS AllLocationLinks
            INNER JOIN
                (SELECT ID FROM dbo.Location WHERE Terminal = 0 AND OffEdge = 0) L
            ON AllLocationLinks.LocationID = L.ID
            WHERE AllLocationLinks.NumLinks <= 1
            ORDER BY ID;
    END');
END
GO

IF OBJECT_ID(N'dbo.SelectUnfinishedStructureBranchesWithPosition', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.SelectUnfinishedStructureBranchesWithPosition
        @StructureID bigint
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT ID, X, Y, Z, Radius FROM
            (SELECT LocationID, COUNT(LocationID) AS NumLinks FROM
                (
                    SELECT A AS LocationID FROM dbo.LocationLink
                    WHERE A IN (SELECT L.ID FROM dbo.Location L WHERE L.ParentID = @StructureID)
                    UNION ALL
                    SELECT B AS LocationID FROM dbo.LocationLink
                    WHERE B IN (SELECT L.ID FROM dbo.Location L WHERE L.ParentID = @StructureID)
                ) AS LinkedIDs
                GROUP BY LocationID) AS AllLocationLinks
            INNER JOIN
                (SELECT ID, X, Y, Z, Radius FROM dbo.Location WHERE Terminal = 0 AND OffEdge = 0) L
            ON AllLocationLinks.LocationID = L.ID
            WHERE AllLocationLinks.NumLinks <= 1
            ORDER BY ID;
    END');
END
GO

-- Lean SplitStructure: BFS via LocationLink within the keep structure, then
-- clone the structure row and re-parent the split subgraph. Child-structure
-- reassignment from the full SSDT proc is omitted (not needed for smoke tests).
IF OBJECT_ID(N'dbo.SplitStructure', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.SplitStructure
        @LocationIDOfSplitStructure bigint,
        @SplitStructureID bigint OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET @SplitStructureID = 0;

        DECLARE @KeepStructureID bigint =
            (SELECT ParentID FROM dbo.Location WHERE ID = @LocationIDOfSplitStructure);
        IF @KeepStructureID IS NULL
            THROW 50000, N''Location not found'', 1;

        IF OBJECT_ID(''tempdb..#LocationLinkPool'') IS NOT NULL DROP TABLE #LocationLinkPool;
        IF OBJECT_ID(''tempdb..#LocationsInSplitSubGraph'') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph;
        IF OBJECT_ID(''tempdb..#LocationsInKeepSubGraph'') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph;

        SELECT LL.A, LL.B
        INTO #LocationLinkPool
        FROM dbo.LocationLink LL
        INNER JOIN dbo.Location LA ON LA.ID = LL.A
        INNER JOIN dbo.Location LB ON LB.ID = LL.B
        WHERE LA.ParentID = @KeepStructureID AND LB.ParentID = @KeepStructureID;

        CREATE TABLE #LocationsInSplitSubGraph (ID bigint PRIMARY KEY);
        INSERT INTO #LocationsInSplitSubGraph (ID) VALUES (@LocationIDOfSplitStructure);

        DECLARE @RowsAdded bigint = 1;
        WHILE @RowsAdded > 0
        BEGIN
            INSERT INTO #LocationsInSplitSubGraph (ID)
            SELECT DISTINCT Candidate.ID
            FROM (
                SELECT B AS ID FROM #LocationLinkPool WHERE A IN (SELECT ID FROM #LocationsInSplitSubGraph)
                UNION
                SELECT A AS ID FROM #LocationLinkPool WHERE B IN (SELECT ID FROM #LocationsInSplitSubGraph)
            ) Candidate
            WHERE Candidate.ID NOT IN (SELECT ID FROM #LocationsInSplitSubGraph);

            SET @RowsAdded = @@ROWCOUNT;

            DELETE LLP
            FROM #LocationLinkPool LLP
            INNER JOIN #LocationsInSplitSubGraph SA ON SA.ID = LLP.A
            INNER JOIN #LocationsInSplitSubGraph SB ON SB.ID = LLP.B;
        END

        SELECT ID INTO #LocationsInKeepSubGraph
        FROM dbo.Location
        WHERE ParentID = @KeepStructureID
          AND ID NOT IN (SELECT ID FROM #LocationsInSplitSubGraph);

        IF (SELECT COUNT(*) FROM #LocationsInKeepSubGraph) = 0
            THROW 50000, N''The split structure is connected to the entire keep cell. Break a location link to create two subgraphs and try again'', 1;

        INSERT INTO dbo.Structure (TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified)
        SELECT TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified
        FROM dbo.Structure WHERE ID = @KeepStructureID;
        SET @SplitStructureID = SCOPE_IDENTITY();

        UPDATE L
        SET ParentID = @SplitStructureID
        FROM dbo.Location L
        INNER JOIN #LocationsInSplitSubGraph S ON S.ID = L.ID;

        IF OBJECT_ID(''tempdb..#LocationLinkPool'') IS NOT NULL DROP TABLE #LocationLinkPool;
        IF OBJECT_ID(''tempdb..#LocationsInSplitSubGraph'') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph;
        IF OBJECT_ID(''tempdb..#LocationsInKeepSubGraph'') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph;
    END');
END
GO

IF OBJECT_ID(N'dbo.SplitStructureAtLocationLink', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.SplitStructureAtLocationLink
        @LocationIDOfKeepStructure bigint,
        @LocationIDOfSplitStructure bigint,
        @SplitStructureID bigint OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        SET @SplitStructureID = 0;

        IF (0 = (SELECT COUNT(*) FROM dbo.LocationLink
                 WHERE (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure)
                    OR (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure)))
            THROW 50000, N''The Split and Keep Location IDs must be linked'', 1;

        BEGIN TRANSACTION split_at_link;
            DELETE FROM dbo.LocationLink
            WHERE (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure)
               OR (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure);

            EXEC dbo.SplitStructure @LocationIDOfSplitStructure, @SplitStructureID OUTPUT;
        COMMIT TRANSACTION split_at_link;
    END');
END
GO

-- Seed one StructureType → Structure → Location when the DB is empty (ids become 1).
IF NOT EXISTS (SELECT 1 FROM dbo.StructureType)
BEGIN
    INSERT INTO dbo.StructureType ([Name], [Notes], [MarkupType], [Code], [Username])
    VALUES (N'Test Neuron', N'Seed type for gRPC tests', N'Point', N'N', N'seed');

    INSERT INTO dbo.Structure ([TypeID], [Notes], [Label], [Username], [Confidence])
    VALUES (1, N'Seed structure', 'seed-1', N'seed', 0.5);

    INSERT INTO dbo.Location ([ParentID], [Z], [TypeCode], [Username], [MosaicShape], [VolumeShape])
    VALUES (
        1,
        1,
        1,
        N'seed',
        geometry::STGeomFromText('POINT (100 200)', 0),
        geometry::STGeomFromText('POINT (100 200)', 0)
    );
END
GO
