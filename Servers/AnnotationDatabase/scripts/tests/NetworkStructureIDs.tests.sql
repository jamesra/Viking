-- Tests for dbo.NetworkStructureIDs. Throwaway test database only (AnnotationTest in the
-- annotation-sql container); every row and object it adds is rolled back at the end.
--
-- Run (bash; the container mounts this scripts folder read-only at /scripts):
--   docker exec viking-annotation-sql sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
--     -P "$MSSQL_SA_PASSWORD" -C -b -t 120 -d AnnotationTest -i /scripts/tests/NetworkStructureIDs.tests.sql'
--
-- Passes only if it prints "NetworkStructureIDs tests passed.". An assertion failure raises an
-- error (sqlcmd -b exits non-zero). -t 120 turns a hop loop that never ends (the bug the early exit
-- fixed: @Hops is request input up to Int32.MaxValue) into "Timeout expired" instead of a hang;
-- sqlcmd 18 still exits 0 on a timeout, so check for the passed line, not only the exit code.

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @Base bigint = 900000000000000000;
DECLARE @Big int = 2147483647;
DECLARE @t datetime2;

SET IDENTITY_INSERT dbo.StructureType ON;
INSERT dbo.StructureType (ID, Name) VALUES (@Base, N'NetworkStructureIDs test');
SET IDENTITY_INSERT dbo.StructureType OFF;

-- Hand-built chain: cells C1..C5 (IDs @Base+1..+5), each with one child K (ID +101..+105).
-- Links K1->K2, K3->K2 (reverse direction), K3->K4. C5 is isolated.
SET IDENTITY_INSERT dbo.Structure ON;
INSERT dbo.Structure (ID, TypeID, ParentID)
    SELECT @Base + n, @Base, NULL FROM (VALUES (1), (2), (3), (4), (5)) V(n);
INSERT dbo.Structure (ID, TypeID, ParentID)
    SELECT @Base + 100 + n, @Base, @Base + n FROM (VALUES (1), (2), (3), (4), (5)) V(n);
SET IDENTITY_INSERT dbo.Structure OFF;
INSERT dbo.StructureLink (SourceID, TargetID)
    VALUES (@Base + 101, @Base + 102), (@Base + 103, @Base + 102), (@Base + 103, @Base + 104);

DECLARE @C1 dbo.integer_list, @C5 dbo.integer_list, @Missing dbo.integer_list, @Empty dbo.integer_list;
INSERT @C1 VALUES (@Base + 1);
INSERT @C5 VALUES (@Base + 5);
INSERT @Missing VALUES (@Base + 99999);

DECLARE @Expected TABLE (Hops int, ID bigint);
INSERT @Expected
    SELECT h.Hops, @Base + c.n FROM (VALUES (-1), (0), (1), (2), (3), (4), (@Big)) h(Hops)
    CROSS JOIN (VALUES (1), (2), (3), (4)) c(n)
    WHERE c.n <= CASE WHEN h.Hops < 0 THEN 1 WHEN h.Hops > 3 THEN 4 ELSE h.Hops + 1 END;

DECLARE @h int;
DECLARE HopCases CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT Hops FROM @Expected;
OPEN HopCases;
FETCH NEXT FROM HopCases INTO @h;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT ID FROM dbo.NetworkStructureIDs(@C1, @h) EXCEPT SELECT ID FROM @Expected WHERE Hops = @h)
       OR EXISTS (SELECT ID FROM @Expected WHERE Hops = @h EXCEPT SELECT ID FROM dbo.NetworkStructureIDs(@C1, @h))
    BEGIN
        DECLARE @msg nvarchar(200) = CONCAT(N'Chain from C1 with hops=', @h, N' returned the wrong set.');
        THROW 50001, @msg, 1;
    END
    FETCH NEXT FROM HopCases INTO @h;
END
CLOSE HopCases;
DEALLOCATE HopCases;

-- Seeds whose frontier empties at once must return right away even for Int32.MaxValue hops.
SET @t = SYSDATETIME();
IF (SELECT COUNT(*) FROM dbo.NetworkStructureIDs(@Empty, @Big)) <> 0
    THROW 50002, N'Empty seed set returned rows.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.NetworkStructureIDs(@C5, @Big) HAVING COUNT(*) = 1 AND MIN(ID) = @Base + 5)
    THROW 50003, N'Isolated cell C5 should return only itself.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.NetworkStructureIDs(@Missing, @Big) HAVING COUNT(*) = 1 AND MIN(ID) = @Base + 99999)
    THROW 50004, N'A seed ID with no Structure row should be returned unchanged.', 1;
IF DATEDIFF(ms, @t, SYSDATETIME()) > 10000
    THROW 50005, N'Int32.MaxValue hops on an exhausted network took over 10 s.', 1;

-- Generated network: 300 cells, 0-3 children each, links between pseudo-randomly chosen children.
-- Each productive hop adds at least one cell, so hops = cell count reaches the fixpoint; MaxValue
-- must give the same set, and every hop count's set must contain the previous one's.
DECLARE @Cells int = 300;
SET IDENTITY_INSERT dbo.Structure ON;
WITH N AS (SELECT TOP (@Cells) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects)
INSERT dbo.Structure (ID, TypeID, ParentID) SELECT @Base + 1000 + n, @Base, NULL FROM N;
WITH N AS (SELECT TOP (@Cells) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.all_objects)
INSERT dbo.Structure (ID, TypeID, ParentID)
    SELECT @Base + 100000 + N.n * 4 + k.k, @Base, @Base + 1000 + N.n
    FROM N CROSS JOIN (VALUES (0), (1), (2)) k(k) WHERE k.k < (N.n * 7) % 4;
SET IDENTITY_INSERT dbo.Structure OFF;

DECLARE @Children TABLE (i int IDENTITY(0, 1), ID bigint);
INSERT @Children (ID) SELECT ID FROM dbo.Structure WHERE ID > @Base + 100000 AND ID < @Base + 200000 ORDER BY ID;
DECLARE @ChildCount int = (SELECT COUNT(*) FROM @Children);
INSERT dbo.StructureLink (SourceID, TargetID)
    SELECT DISTINCT A.ID, B.ID FROM @Children A
    INNER JOIN @Children B ON B.i = (A.i * 37 + 11) % @ChildCount
    WHERE A.i % 3 <> 0 AND A.ID <> B.ID;

DECLARE @Seed int = 1;
WHILE @Seed <= 300
BEGIN
    DECLARE @S dbo.integer_list;
    DELETE @S;
    INSERT @S VALUES (@Base + 1000 + @Seed);

    SET @t = SYSDATETIME();
    IF EXISTS (SELECT ID FROM dbo.NetworkStructureIDs(@S, @Big) EXCEPT SELECT ID FROM dbo.NetworkStructureIDs(@S, @Cells))
       OR EXISTS (SELECT ID FROM dbo.NetworkStructureIDs(@S, @Cells) EXCEPT SELECT ID FROM dbo.NetworkStructureIDs(@S, @Big))
        THROW 50006, N'Int32.MaxValue hops differs from the fixpoint hop count.', 1;
    IF DATEDIFF(ms, @t, SYSDATETIME()) > 10000
        THROW 50007, N'Int32.MaxValue hops on the generated network took over 10 s.', 1;

    SET @h = 0;
    WHILE @h < 6
    BEGIN
        IF EXISTS (SELECT ID FROM dbo.NetworkStructureIDs(@S, @h) EXCEPT SELECT ID FROM dbo.NetworkStructureIDs(@S, @h + 1))
            THROW 50008, N'A larger hop count lost cells.', 1;
        SET @h += 1;
    END
    SET @Seed += 37;
END

ROLLBACK;
PRINT N'NetworkStructureIDs tests passed.';
