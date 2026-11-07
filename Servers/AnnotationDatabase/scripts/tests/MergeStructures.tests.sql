-- Tests for dbo.MergeStructures. Throwaway test database only (AnnotationTest in the
-- annotation-sql container); every row it adds is rolled back at the end.
--
-- Run (bash; the container mounts this scripts folder read-only at /scripts):
--   docker exec viking-annotation-sql sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
--     -P "$MSSQL_SA_PASSWORD" -C -b -d AnnotationTest -i /scripts/tests/MergeStructures.tests.sql'
--
-- Passes only if it prints "MergeStructures tests passed.". An assertion failure raises an error
-- (sqlcmd -b exits non-zero). It tests whichever MergeStructures the database holds: the
-- minimal-schema copy, or the dbo/Stored Procedures one after a .sqlproj publish.
--
-- It refuses to run in any database but AnnotationTest: the IDENTITY_INSERT rows below move the
-- Structure and StructureType identity seeds to 9e17, and a ROLLBACK does not move them back.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'AnnotationTest'
    THROW 50000, N'MergeStructures.tests.sql runs only in the throwaway AnnotationTest database.', 1;

BEGIN TRAN;

DECLARE @Base bigint = 900000000000001000;
DECLARE @crlf nvarchar(2) = CHAR(13) + CHAR(10);

SET IDENTITY_INSERT dbo.StructureType ON;
INSERT dbo.StructureType (ID, Name) VALUES (@Base, N'MergeStructures test');
SET IDENTITY_INSERT dbo.StructureType OFF;

-- Pairs (keep @Base+10n+1, merge @Base+10n+2), keep Notes / merge Notes:
--   n=1 NULL / text      n=2 text / text      n=3 NULL / NULL      n=4 text / ''
-- Merge structure of pair 1 also has a child (@Base+19) that must move to the keep structure.
SET IDENTITY_INSERT dbo.Structure ON;
INSERT dbo.Structure (ID, TypeID, ParentID, Notes) VALUES
    (@Base + 11, @Base, NULL, NULL),     (@Base + 12, @Base, NULL, N'merged notes 1'),
    (@Base + 21, @Base, NULL, N'kept 2'), (@Base + 22, @Base, NULL, N'merged notes 2'),
    (@Base + 31, @Base, NULL, NULL),     (@Base + 32, @Base, NULL, NULL),
    (@Base + 41, @Base, NULL, N'kept 4'), (@Base + 42, @Base, NULL, N'');
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES (@Base + 19, @Base, @Base + 12);
SET IDENTITY_INSERT dbo.Structure OFF;

DECLARE @n int = 1, @Keep bigint, @Merge bigint;
WHILE @n <= 4
BEGIN
    SET @Keep = @Base + 10 * @n + 1;
    SET @Merge = @Keep + 1;
    EXEC dbo.MergeStructures @KeepStructureID = @Keep, @MergeStructureID = @Merge;
    SET @n += 1;
END

DECLARE @Block1 nvarchar(max) = CONCAT(@crlf, N'*****BEGIN MERGE FROM ', @Base + 12, N'*****', @crlf,
    N'merged notes 1', @crlf, N'*****END MERGE FROM ', @Base + 12, N'*****', @crlf);
DECLARE @Block2 nvarchar(max) = CONCAT(@crlf, N'*****BEGIN MERGE FROM ', @Base + 22, N'*****', @crlf,
    N'merged notes 2', @crlf, N'*****END MERGE FROM ', @Base + 22, N'*****', @crlf);

-- A keep structure with NULL Notes must still receive the merged notes (they were lost as NULL).
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 11 AND Notes = @Block1)
    THROW 50001, N'Merging into a structure with NULL Notes lost the merged notes.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 21 AND Notes = N'kept 2' + @Block2)
    THROW 50002, N'Merged notes were not appended after the kept notes.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 31 AND Notes IS NULL)
    THROW 50003, N'Merging a structure without notes changed NULL kept Notes.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 41 AND Notes = N'kept 4')
    THROW 50004, N'Merging a structure with empty notes changed the kept Notes.', 1;

IF EXISTS (SELECT 1 FROM dbo.Structure WHERE ID IN (@Base + 12, @Base + 22, @Base + 32, @Base + 42))
    THROW 50005, N'A merged structure was not deleted.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 19 AND ParentID = @Base + 11)
    THROW 50006, N'The merged structure''s child was not moved to the keep structure.', 1;

-- Refused merges: the keep structure is the merged structure or lies below it, so moving the merged structure's
-- children to it would make it its own parent or close a cycle. Each must fail with 50000 and change nothing.
--   n=5 keep @Base+51 is a child of merge @Base+52, which has another child @Base+59
--   n=6 keep @Base+61 is a grandchild of merge @Base+62 (through @Base+63)
--   n=7 keep and merge are both @Base+71
SET IDENTITY_INSERT dbo.Structure ON;
INSERT dbo.Structure (ID, TypeID, ParentID, Notes) VALUES
    (@Base + 52, @Base, NULL, N'merge 5'), (@Base + 62, @Base, NULL, N'merge 6'), (@Base + 71, @Base, NULL, N'self 7');
INSERT dbo.Structure (ID, TypeID, ParentID, Notes) VALUES
    (@Base + 51, @Base, @Base + 52, N'keep 5'), (@Base + 59, @Base, @Base + 52, NULL), (@Base + 63, @Base, @Base + 62, NULL);
INSERT dbo.Structure (ID, TypeID, ParentID, Notes) VALUES (@Base + 61, @Base, @Base + 63, N'keep 6');
SET IDENTITY_INSERT dbo.Structure OFF;

DECLARE @Before TABLE (ID bigint, ParentID bigint NULL, Notes nvarchar(max) NULL);
INSERT @Before SELECT ID, ParentID, Notes FROM dbo.Structure WHERE ID BETWEEN @Base + 51 AND @Base + 79;

DECLARE @Refused TABLE (Keep bigint, [Merge] bigint);
INSERT @Refused VALUES (@Base + 51, @Base + 52), (@Base + 61, @Base + 62), (@Base + 71, @Base + 71);

-- XACT_ABORT OFF so a refusal caught below leaves the test transaction committable.
SET XACT_ABORT OFF;
DECLARE @Error int;
WHILE EXISTS (SELECT 1 FROM @Refused)
BEGIN
    SELECT TOP (1) @Keep = Keep, @Merge = [Merge] FROM @Refused ORDER BY Keep;
    DELETE @Refused WHERE Keep = @Keep;
    SET @Error = 0;
    BEGIN TRY
        EXEC dbo.MergeStructures @KeepStructureID = @Keep, @MergeStructureID = @Merge;
    END TRY
    BEGIN CATCH
        SET @Error = ERROR_NUMBER();
    END CATCH
    IF @Error <> 50000 OR XACT_STATE() <> 1
    BEGIN
        SET XACT_ABORT ON;
        DECLARE @Unrefused nvarchar(200) = CONCAT(N'Merging ', @Merge, N' into ', @Keep, N' was not refused with 50000 (got ', @Error, N').');
        THROW 50007, @Unrefused, 1;
    END
END
SET XACT_ABORT ON;

IF EXISTS (SELECT ID, ParentID, Notes FROM @Before
           EXCEPT SELECT ID, ParentID, Notes FROM dbo.Structure WHERE ID BETWEEN @Base + 51 AND @Base + 79)
    THROW 50008, N'A refused merge changed, moved or deleted a structure.', 1;

-- Allowed merges the walk must not refuse:
--   n=8 keep @Base+81 is the parent of merge @Base+82 (whose child @Base+89 moves to the keep)
--   n=9 keep @Base+91 and merge @Base+92 are siblings under @Base+93
--   n=10 keep @Base+101 sits on a stored parent cycle (@Base+101 <-> @Base+103) that does not reach merge @Base+102
SET IDENTITY_INSERT dbo.Structure ON;
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES
    (@Base + 81, @Base, NULL), (@Base + 93, @Base, NULL), (@Base + 101, @Base, NULL), (@Base + 102, @Base, NULL),
    (@Base + 103, @Base, @Base + 101);
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES
    (@Base + 82, @Base, @Base + 81), (@Base + 91, @Base, @Base + 93), (@Base + 92, @Base, @Base + 93);
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES (@Base + 89, @Base, @Base + 82);
SET IDENTITY_INSERT dbo.Structure OFF;
UPDATE dbo.Structure SET ParentID = @Base + 103 WHERE ID = @Base + 101;

SET @n = 8;
WHILE @n <= 10
BEGIN
    SET @Keep = @Base + 10 * @n + 1;
    SET @Merge = @Keep + 1;
    EXEC dbo.MergeStructures @KeepStructureID = @Keep, @MergeStructureID = @Merge;
    SET @n += 1;
END

IF EXISTS (SELECT 1 FROM dbo.Structure WHERE ID IN (@Base + 82, @Base + 92, @Base + 102))
    THROW 50009, N'A merge into the merged structure''s parent, a sibling or a structure on a stored cycle was refused.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Structure WHERE ID = @Base + 89 AND ParentID = @Base + 81)
    THROW 50010, N'Merging a structure into its parent did not move its child to the parent.', 1;

ROLLBACK;
PRINT N'MergeStructures tests passed.';
