-- Seed rows the gRPC integration tests assume (WebAnnotationModel.gRPC.Tests, gRPC_Tests): StructureType 1,
-- Structure 1 and Location 1 on section 1 at mosaic (100, 200). Reset-AnnotationTestDatabase.ps1 runs it on a newly
-- created AnnotationTest, so the identity columns hand out 1. It refuses a database that already has rows.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.StructureType) OR EXISTS (SELECT 1 FROM dbo.Structure) OR EXISTS (SELECT 1 FROM dbo.Location)
    THROW 50000, N'test-seed.sql: the database already has annotations; seed only a newly created AnnotationTest.', 1;

BEGIN TRANSACTION;

INSERT INTO dbo.StructureType ([Name], [Notes], [MarkupType], [Code], [Username])
VALUES (N'Test Neuron', N'Seed type for gRPC tests', N'Point', N'N', N'seed');

INSERT INTO dbo.Structure ([TypeID], [Notes], [Label], [Username], [Confidence])
VALUES (1, N'Seed structure', 'seed-1', N'seed', 0.5);

INSERT INTO dbo.Location ([ParentID], [Z], [TypeCode], [Username], [MosaicShape], [VolumeShape])
VALUES (1, 1, 1, N'seed',
        geometry::STGeomFromText('POINT (100 200)', 0),
        geometry::STGeomFromText('POINT (100 200)', 0));

COMMIT TRANSACTION;

IF (SELECT MAX(ID) FROM dbo.Location) <> 1 OR (SELECT MAX(ID) FROM dbo.Structure) <> 1 OR (SELECT MAX(ID) FROM dbo.StructureType) <> 1
    THROW 50001, N'test-seed.sql: the seed rows did not get ID 1.', 1;
