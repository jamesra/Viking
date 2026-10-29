-- Tests for the dbo.Structure_delete trigger, which logs every deleted structure in dbo.DeletedStructures
-- (the gRPC StructureService's DeletedIds on incremental structure syncs). Throwaway test database only
-- (AnnotationTest in the annotation-sql container); every row it adds is rolled back at the end.
--
-- Run (bash; the container mounts this scripts folder read-only at /scripts):
--   docker exec viking-annotation-sql sh -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa \
--     -P "$MSSQL_SA_PASSWORD" -C -b -d AnnotationTest -i /scripts/tests/DeletedStructures.tests.sql'
--
-- Passes only if it prints "DeletedStructures tests passed.". An assertion failure raises an error
-- (sqlcmd -b exits non-zero). It covers each way a structure is deleted: DeepDeleteStructure (the
-- structure and its child), MergeStructures (the merged structure), and a plain DELETE as the WCF
-- service (EF6) issues it, plus an ID that is already logged. It runs on a minimal-schema.sql database
-- or one built by CreateUpdateDatabase.sql (DBVersion 88 or later).
--
-- It refuses to run in any database but AnnotationTest: the IDENTITY_INSERT rows below move the
-- Structure and StructureType identity seeds to 9e17, and a ROLLBACK does not move them back.

-- sqlcmd starts with QUOTED_IDENTIFIER OFF; SqlClient (EF6, EF Core) turns it ON, and the plain DELETE below
-- cascades to Location, whose persisted computed columns need it ON (Msg 1934 otherwise).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'AnnotationTest'
    THROW 50000, N'DeletedStructures.tests.sql runs only in the throwaway AnnotationTest database.', 1;

BEGIN TRAN;

DECLARE @Base bigint = 900000000000002000;
DECLARE @Start datetime = DATEADD(second, -1, GETUTCDATE());
DECLARE @LongAgo datetime = '2000-01-01';

-- HotKey is given because the CreateUpdateDatabase.sql default (CHAR(0) as '\0') does not fit char(1).
SET IDENTITY_INSERT dbo.StructureType ON;
INSERT dbo.StructureType (ID, Name, HotKey) VALUES (@Base, N'DeletedStructures test', 'z');
SET IDENTITY_INSERT dbo.StructureType OFF;

-- Deep delete: parent @Base+1 with child @Base+2.  Merge: keep @Base+11, merge @Base+12.
-- Plain DELETE of two rows: @Base+21, @Base+22.  Already logged: @Base+31 (logged @LongAgo), @Base+32.
SET IDENTITY_INSERT dbo.Structure ON;
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES
    (@Base + 1, @Base, NULL),  (@Base + 11, @Base, NULL), (@Base + 12, @Base, NULL),
    (@Base + 21, @Base, NULL), (@Base + 22, @Base, NULL), (@Base + 31, @Base, NULL), (@Base + 32, @Base, NULL);
INSERT dbo.Structure (ID, TypeID, ParentID) VALUES (@Base + 2, @Base, @Base + 1);
SET IDENTITY_INSERT dbo.Structure OFF;

INSERT dbo.DeletedStructures (ID, DeletedOn) VALUES (@Base + 31, @LongAgo);

IF EXISTS (SELECT 1 FROM dbo.DeletedStructures WHERE ID BETWEEN @Base + 1 AND @Base + 30)
    THROW 50001, N'A test structure was logged as deleted before any delete ran.', 1;

DECLARE @Id bigint = @Base + 1, @Keep bigint = @Base + 11, @Merge bigint = @Base + 12;
EXEC dbo.DeepDeleteStructure @DeleteID = @Id;
IF (SELECT COUNT(*) FROM dbo.DeletedStructures WHERE ID IN (@Base + 1, @Base + 2) AND DeletedOn >= @Start) <> 2
    THROW 50002, N'DeepDeleteStructure did not log the structure and its child.', 1;

EXEC dbo.MergeStructures @KeepStructureID = @Keep, @MergeStructureID = @Merge;
IF NOT EXISTS (SELECT 1 FROM dbo.DeletedStructures WHERE ID = @Base + 12 AND DeletedOn >= @Start)
    THROW 50003, N'MergeStructures did not log the merged structure.', 1;
IF EXISTS (SELECT 1 FROM dbo.DeletedStructures WHERE ID = @Base + 11)
    THROW 50004, N'MergeStructures logged the keep structure, which still exists.', 1;

DELETE dbo.Structure WHERE ID IN (@Base + 21, @Base + 22);
IF (SELECT COUNT(*) FROM dbo.DeletedStructures WHERE ID IN (@Base + 21, @Base + 22) AND DeletedOn >= @Start) <> 2
    THROW 50005, N'A plain DELETE of two structures did not log both.', 1;

-- An ID already logged (only a reused ID, as IDENTITY_INSERT makes, can be) must not fail the delete
-- on the primary key; the other row of the same statement is still logged.
DELETE dbo.Structure WHERE ID IN (@Base + 31, @Base + 32);
IF EXISTS (SELECT 1 FROM dbo.Structure WHERE ID IN (@Base + 31, @Base + 32))
    THROW 50006, N'Deleting a structure whose ID was already logged did not delete it.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.DeletedStructures WHERE ID = @Base + 31 AND DeletedOn = @LongAgo)
    THROW 50007, N'The row for an ID already logged was changed instead of skipped.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.DeletedStructures WHERE ID = @Base + 32 AND DeletedOn >= @Start)
    THROW 50008, N'The new ID deleted alongside an already-logged one was not logged.', 1;

ROLLBACK;
PRINT N'DeletedStructures tests passed.';
