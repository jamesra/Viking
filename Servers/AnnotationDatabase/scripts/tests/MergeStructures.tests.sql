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

ROLLBACK;
PRINT N'MergeStructures tests passed.';
