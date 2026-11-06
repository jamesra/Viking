-- Tests for DF_StructureType_HotKey: a StructureType inserted without a HotKey gets the NUL character, the value
-- the clients write for "no hotkey". Throwaway test database only (AnnotationTest in the annotation-sql container);
-- the row it adds is rolled back at the end.
--
-- Run (bash; the container mounts this scripts folder read-only at /scripts):
--   docker exec viking-annotation-sql sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
--     -P "$MSSQL_SA_PASSWORD" -C -b -d AnnotationTest -i /scripts/tests/StructureTypeHotKey.tests.sql'
--
-- Passes only if it prints "StructureTypeHotKey tests passed.". An assertion failure raises an error
-- (sqlcmd -b exits non-zero). It runs on a minimal-schema.sql database or one built by CreateUpdateDatabase.sql
-- (DBVersion 91 or later).

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_WARNINGS ON;

IF DB_NAME() <> N'AnnotationTest'
    THROW 50000, N'StructureTypeHotKey.tests.sql runs only in the throwaway AnnotationTest database.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
               INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
               WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StructureType') AND c.name = N'HotKey'
                 AND dc.definition = N'(char((0)))')
    THROW 50001, N'StructureType.HotKey does not default to CHAR(0).', 1;

BEGIN TRAN;

DECLARE @Inserted TABLE (ID bigint);
INSERT dbo.StructureType (Name) OUTPUT inserted.ID INTO @Inserted VALUES (N'StructureTypeHotKey test');

-- ASCII, not a string comparison: collations ignore CHAR(0) when comparing strings.
IF ISNULL((SELECT ASCII(HotKey) FROM dbo.StructureType WHERE ID = (SELECT ID FROM @Inserted)), -1) <> 0
    THROW 50002, N'A StructureType inserted without a HotKey did not get CHAR(0).', 1;

ROLLBACK TRAN;

PRINT N'StructureTypeHotKey tests passed.';
