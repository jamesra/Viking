using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Viking.DataModel.Annotation.Migrations
{
    public partial class MemoryMappedTypes : Migration
    {
        private readonly static string MemOptimizedFilegroup = "MemOptimizedData";
        protected override void Up(MigrationBuilder migrationBuilder)
        {  
			if (migrationBuilder.IsSqlServer())
            {
                ////Create Memory optmized table files
                migrationBuilder.Sql(
                    $" ALTER DATABASE CURRENT ADD FILEGROUP {MemOptimizedFilegroup} CONTAINS MEMORY_OPTIMIZED_DATA ",
                    suppressTransaction: true);

                string query =
				  @"declare @Query VARCHAR(4096)
                    declare @filename varchar(1024)
                    SELECT TOP 1 @filename=physical_name FROM sys.master_files
				                    where database_id = DB_ID() and type=0

					declare @dirSep char
					IF CHARINDEX('/', @filename) > 0
						set @dirSep = '/'
					ELSE
						set @dirSep = '\' 

                    declare @dirname varchar(1024)
                    SET @dirname = LEFT(@filename, LEN(@filename) - CHARINDEX(@dirSep, REVERSE(@filename)) + 1)
					" +
                    $"set @Query = 'ALTER DATABASE CURRENT ADD FILE (name=''' + DB_NAME() + '_{MemOptimizedFilegroup}'', filename=''' +  @dirname + DB_NAME() + '_{MemOptimizedFilegroup}'') TO FILEGROUP {MemOptimizedFilegroup}'\n" +
                    "EXEC(@Query)";

                migrationBuilder.Sql(query,
                    suppressTransaction: true);
				  
				migrationBuilder.Sql(@"ALTER TABLE Location
										ADD CONSTRAINT chk_Location_Width CHECK(
										(0 = TypeCode AND Width IS NULL) OR
										(1 = TypeCode AND Width IS NULL) OR
										(2 = TypeCode AND Width IS NULL) OR
										(3 = TypeCode AND Width IS NOT NULL) OR
										(4 = TypeCode AND Width IS NULL) OR
										(5 = TypeCode AND Width IS NOT NULL) OR
										(6 = TypeCode AND Width IS NULL) OR
										(7 = TypeCode AND Width IS NOT NULL)
									)");

				AddTypes(migrationBuilder);
				AddTriggers(migrationBuilder);
				AddIndicies(migrationBuilder);			
				AddStoredProcedures(migrationBuilder);
				AddFunctions(migrationBuilder);
            }
        }

		private void AddTypes(MigrationBuilder migrationBuilder){
			migrationBuilder.Sql(@"CREATE TYPE [dbo].[integer_list] AS TABLE(
                                    [ID][bigint] NOT NULL,
                                    PRIMARY KEY NONCLUSTERED
                                    ( 
                                        [ID] ASC
                                    )
                                )
                                WITH(MEMORY_OPTIMIZED = ON)", suppressTransaction: true);

			migrationBuilder.Sql(@"CREATE TYPE [dbo].[udtParentChildIDMap] AS TABLE(
			                            [ID] [bigint] NOT NULL,
			                            [ParentID] [bigint] NOT NULL,
			                            INDEX [udtParentChildIDMap_idx1] NONCLUSTERED 
			                            (
				                            [ID] ASC
			                            ),
			                            INDEX [udtParentChildIDMap_ParentID_idx] NONCLUSTERED 
			                            (
				                            [ParentID] ASC
			                            )
		                            )
		                            WITH (MEMORY_OPTIMIZED=ON)", suppressTransaction: true);

			migrationBuilder.Sql(@"CREATE TYPE [dbo].[udtLinks] AS TABLE(
					                    [SourceID] [bigint] NOT NULL,
					                    [TargetID] [bigint] NOT NULL,
					                    PRIMARY KEY NONCLUSTERED 
					                    (
						                    [SourceID] ASC,
						                    [TargetID] ASC
					                    ),
					                    INDEX [SourceID_idx] NONCLUSTERED 
					                    (
						                    [SourceID] ASC
					                    ),
					                    INDEX [TargetID_idx] NONCLUSTERED 
					                    (
						                    [TargetID] ASC
					                    )
				                    )
				                    WITH(MEMORY_OPTIMIZED=ON)", suppressTransaction: true);
		}

		private void AddIndicies(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql(
				@"  CREATE SPATIAL INDEX [MosaicShape_Index] ON [dbo].[Location]
                    (
	                    [MosaicShape]
                    )USING  GEOMETRY_AUTO_GRID 
                    WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
                    CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                    GO");

			migrationBuilder.Sql(
				@"  CREATE SPATIAL INDEX [VolumeShape_Index] ON [dbo].[Location]
                    (
	                    [VolumeShape]
                    )USING  GEOMETRY_AUTO_GRID 
                    WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
                    CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
		}

		private void AddTriggers(MigrationBuilder migrationBuilder)
        { 
			migrationBuilder.Sql(
				@"CREATE TRIGGER [dbo].[Location_update]  
                ON[dbo].[Location] 
                FOR UPDATE 
                AS 
                    SET NOCOUNT ON;
                    Update dbo.Location 
                    Set LastModified = (GETUTCDATE())  
                    WHERE ID in (SELECT ID FROM inserted)");

			migrationBuilder.Sql(
				@"CREATE TRIGGER [dbo].[Location_delete] 
	               ON  [dbo].[Location]
	               FOR DELETE
	             AS 
                    SET NOCOUNT ON;
		            INSERT INTO [dbo].[DeletedLocations] (ID)
		            SELECT deleted.ID FROM deleted
		            
		            delete from LocationLink 
			            where A in  (SELECT deleted.ID FROM deleted)
				            or B in (SELECT deleted.ID FROM deleted)");

			migrationBuilder.Sql(
				@"CREATE TRIGGER [dbo].[StructureLink_ReciprocalCheck] 
                    ON  [dbo].[StructureLink]
                    AFTER INSERT, UPDATE
                    AS 
                        SET NOCOUNT ON;
	                    IF ((select count(SLA.SourceID)
		                    from inserted SLA 
		                    JOIN StructureLink SLB 
		                    ON (SLA.SourceID = SLB.TargetID AND SLA.TargetID = SLB.SourceID)) > 0)
		                    BEGIN
			                    RAISERROR(N'Reciprocal structure links are not allowed. Set the bidirectional property on the link instead.',14,1);
			                    ROLLBACK TRANSACTION;
			                    RETURN
		                    END");

			migrationBuilder.Sql(
				@"CREATE TRIGGER [dbo].[StructureType_LastModified] 
	               ON  [dbo].[StructureType]
	               FOR UPDATE
	            AS 
                    -- SET NOCOUNT ON added to prevent extra result sets from
		            -- interfering with SELECT statements.
                    SET NOCOUNT ON;
		            Update dbo.[StructureType]
		            Set LastModified = (SYSUTCDATETIME())
		            WHERE ID in (SELECT ID FROM inserted)
		            
		            ");

			migrationBuilder.Sql(
				@"CREATE TRIGGER [dbo].[Structure_LastModified] 
	               ON  [dbo].[Structure]
	               FOR UPDATE
	            AS 
                    -- SET NOCOUNT ON added to prevent extra result sets from
		            -- interfering with SELECT statements.
		            SET NOCOUNT ON;
		            Update dbo.[Structure]
		            Set LastModified = (SYSUTCDATETIME())
		            WHERE ID in (SELECT ID FROM inserted)
		            ");
		}

		private void AddFunctions(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql(@"CREATE FUNCTION LocationHasTag 
									(
										-- Add the parameters for the function here
										@ID bigint,
										@TagName nvarchar(128)
									)
									RETURNS bit
									AS
									BEGIN
										-- Add the T-SQL statements to compute the return value here
										RETURN
											(SELECT MAX( CASE 
													WHEN N.value('.','nvarchar(128)') LIKE @Tagname THEN 1
													ELSE 0
												END)
												FROM Location
													cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
													WHERE ID = @ID) 
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION NetworkStructureIDs
									(
										-- Add the parameters for the function here
										@IDs integer_list READONLY,
										@Hops int
									)
									RETURNS @CellsInNetwork TABLE 
									(
										-- Add the column definitions for the TABLE variable here
										ID bigint PRIMARY KEY
									)
									AS
									BEGIN
										-- Fill the table variable with the rows for your result set
	
										DECLARE @HopSeedCells integer_list 

										insert into @HopSeedCells select ID from @IDs 
										insert into @CellsInNetwork select ID from @IDs 

										while @Hops > 0
										BEGIN
											DECLARE @HopSeedCellsChildStructures integer_list
											DECLARE @ChildStructurePartners integer_list
											DECLARE @HopCellsFound integer_list
		
											insert into @HopSeedCellsChildStructures
												select distinct Child.ID from Structure Parent
													inner join Structure Child ON Child.ParentID = Parent.ID
													inner join @HopSeedCells Cells ON Cells.ID = Parent.ID
		
											insert into @ChildStructurePartners
												select distinct SL.TargetID from StructureLink SL
													inner join @HopSeedCellsChildStructures C ON C.ID = SL.SourceID
												UNION
												select distinct SL.SourceID from StructureLink SL
													inner join @HopSeedCellsChildStructures C ON C.ID = SL.TargetID
				 
											insert into @HopCellsFound 
												select distinct Parent.ID from Structure Parent
													inner join Structure Child ON Child.ParentID = Parent.ID
													inner join @ChildStructurePartners Partners ON Partners.ID = Child.ID
												where Parent.ID not in (Select ID from @CellsInNetwork union select ID from @HopSeedCells)
		
											delete S from @HopSeedCells S
		
											insert into @HopSeedCells 
												select ID from @HopCellsFound 
												where ID not in (Select ID from @CellsInNetwork)

											insert into @CellsInNetwork select ID from @HopCellsFound 
												where ID not in (Select ID from @CellsInNetwork)
			 

											delete from @ChildStructurePartners
											delete from @HopCellsFound
			 
											set @Hops = @Hops - 1
										END 

										RETURN 
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[NetworkChildStructureIDs]
									(
										-- Add the parameters for the function here
										@IDs integer_list READONLY,
										@Hops int
									)
									RETURNS @ChildStructuresInNetwork TABLE 
									(
										-- Add the column definitions for the TABLE variable here
										ID bigint PRIMARY KEY
									)
									AS
									BEGIN
										-- Fill the table variable with the rows for your result set
										DECLARE @ChildIDsInNetwork integer_list 
	 
										insert into @ChildIDsInNetwork 
											select ChildStruct.ID from Structure S
											inner join NetworkStructureIDs(@IDs, @Hops) N ON S.ID = N.ID
											inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

										insert into @ChildStructuresInNetwork 
											select SL.SourceID as ID from StructureLink SL
												where SL.SourceID in (Select ID from @ChildIDsInNetwork)
											UNION
											select SL.TargetID as ID from StructureLink SL
												where SL.TargetID in (Select ID from @ChildIDsInNetwork)

										RETURN
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[RecursiveSelectChildStructureIDs](
												-- Add the parameters for the stored procedure here
												@IDs integer_list READONLY)
									RETURNS @ChildIDsInNetwork TABLE
									(
										ID bigint PRIMARY KEY
									)
									AS
									BEGIN 	 
										DECLARE @NumSeedStructures int
										DECLARE @SeedStructures integer_list 

										insert into @SeedStructures select ID from @IDs 

										select @NumSeedStructures=count(ID) from @SeedStructures

										while @NumSeedStructures > 0
										BEGIN
											DECLARE @NewChildStructures integer_list 
											insert into @NewChildStructures
												select distinct Child.ID from Structure Child
													inner join @SeedStructures Parents on Parents.ID = Child.ParentID

											delete from @SeedStructures
											insert into @SeedStructures select ID from @NewChildStructures
											select @NumSeedStructures=count(ID) from @SeedStructures

											insert into @ChildIDsInNetwork select ID from @NewChildStructures
											delete from @NewChildStructures
										END
										 
										RETURN 
									END");


			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[SectionLocations](@Z float)
									RETURNS TABLE 
									AS
									RETURN(
 											Select * from Location where Z = @Z
										);");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[SectionLocationsModifiedAfterDate](@Z float, @QueryDate datetime)
									RETURNS TABLE 
									AS
									RETURN(
 											Select * from Location 
											where Z = @Z AND LastModified >= @QueryDate
										);");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[SelectNetworkStructures](
										-- Add the parameters for the stored procedure here
										@IDs integer_list READONLY,
										@Hops int)
									RETURNS TABLE
									AS 
										RETURN select S.* from Structure S 
											inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[SelectNetworkChildStructures]
												-- Add the parameters for the stored procedure here
												(@IDs integer_list READONLY,
												@Hops int)
									RETURNS TABLE
									AS
									RETURN select S.* from Structure S 
											inner join NetworkChildStructureIDs(@IDs, @Hops) N ON N.ID = S.ID");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[SelectNetworkStructureLinks](
												-- Add the parameters for the stored procedure here
												@IDs integer_list READONLY,
												@Hops int)
									RETURNS TABLE
									AS
									RETURN
										select SL.* from StructureLink SL
										INNER JOIN NetworkChildStructureIDs( @IDs, @Hops) NCS ON SL.SourceID = NCS.ID OR SL.TargetID = NCS.ID");

			migrationBuilder.Sql(@"CREATE FUNCTION StructureHasTag 
								(
									-- Add the parameters for the function here
									@StructureID bigint,
									@TagName nvarchar(128)
								)
								RETURNS bit
								AS
								BEGIN
									-- Add the T-SQL statements to compute the return value here
									RETURN
										(SELECT MAX( CASE 
											WHEN N.value('.','nvarchar(128)') LIKE @Tagname THEN 1
											ELSE 0
										END)
										FROM Structure
											cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
											WHERE ID = @StructureID)  
								END");


			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[StructureLocationLinks](@StructureID bigint)
									RETURNS TABLE 
									AS
									RETURN(
 											select LLA.* from  LocationLink LLA 
											inner join Location L ON LLA.A = L.ID
											where L.ParentID = @StructureID
											union
											select LLB.* from LocationLink LLB  
											inner join Location L ON LLB.B = L.ID
											where L.ParentID = @StructureID
											)");

			migrationBuilder.Sql(@"CREATE FUNCTION ufnStructureVolume
									(
										-- Add the parameters for the function here
										@StructureID bigint
									)
									RETURNS float
									AS
									BEGIN
										declare @Area float
										declare @AreaScalar float
										--Measures the area of the PSD
										set @AreaScalar = dbo.XYScale() * dbo.ZScale()

										select top 1 @Area = sum(MosaicShape.STArea()) * @AreaScalar from Location 
										where ParentID = @StructureID
										group by ParentID
	  
										-- Return the result of the function
										RETURN @Area

									END");

			

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[ufnLastStructureMorphologyModification]
									(
										-- Add the parameters for the function here
										@ID bigint
									)
									RETURNS DateTime
									AS
									BEGIN
										-- Declare the return variable here
										DECLARE @ResultVar DateTime

										-- Add the T-SQL statements to compute the return value here
										select @ResultVar = max(Q.LastModified) from (
											select L.LastModified as LastModified from Location L where L.ParentID = @ID
											union
											select LLA.Created as LastModified from Location L 
												inner join LocationLink LLA ON LLA.A = L.ID
												where L.ParentID = @ID
											union
											select S.LastModified as LastModified from Structure S where S.ID = @ID
											) Q
		
										RETURN @ResultVar
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION ufnLastStructureMorphologyModificationRecursive
									(
										-- Add the parameters for the function here
										@ID bigint
									)
									RETURNS DateTime
									AS
									BEGIN
										-- Declare the return variable here
										DECLARE @ResultVar DateTime

										select @ResultVar = max(dbo.ufnLastStructureModification(S.ID)) from Structure S where S.ID = @ID or S.ParentID = @ID
	 
										RETURN @ResultVar
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION [dbo].[ufnLastNetworkModification]
									(
										-- Add the parameters for the function here
										@IDs integer_list READONLY,
										@Hops int
									)
									RETURNS DateTime
									AS
									BEGIN
										-- Declare the return variable here
										DECLARE @ResultVar DateTime
										declare @Network_IDs integer_list

										insert into @Network_IDs 
										select ID from NetworkStructureIDs ( @IDs, @Hops )
										union 
										select ID from NetworkChildStructureIDs( @IDs, @Hops)
	    
										declare @Result DateTime
 
										select @ResultVar = MAX(S.LastModified) from Structure S
															inner join @Network_IDs N on N.ID = S.ID

										RETURN @ResultVar
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION dbo.XYScaleUnits()
									RETURNS varchar(MAX)
									AS 
									-- Returns the scale in the Z axis
									BEGIN
										RETURN 'nm'
									END");

			migrationBuilder.Sql(@"CREATE FUNCTION dbo.ZScaleUnits()
									RETURNS varchar(MAX)
									AS 
									-- Returns the scale in the Z axis
									BEGIN
										RETURN 'nm'
									END");
		}

        private void AddStoredProcedures(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE PROCEDURE DeepDeleteStructure
									-- Add the parameters for the stored procedure here
									@DeleteID bigint
									AS
									BEGIN
									-- SET NOCOUNT ON added to prevent extra result sets from
									-- interfering with SELECT statements.
									SET NOCOUNT ON;
				
									DECLARE @StructuresToDelete integer_list

									INSERT INTO @StructuresToDelete Select ID from Structure where ID = @DeleteID or ParentID = @DeleteID

									delete from LocationLink
									where A in 
									(
									Select ID from Location 
									where ParentID in (Select ID From @StructuresToDelete) ) 

									delete from LocationLink
									where B in 
									(
									Select ID from Location where ParentID in (Select ID From @StructuresToDelete) ) 

									delete from Location
									where ParentID in (Select ID From @StructuresToDelete)

									delete from StructureLink where SourceID in (Select ID From @StructuresToDelete) or TargetID in (Select ID From #StructuresToDelete)

									delete from Structure
									where ParentID=@DeleteID

									delete from Structure
									where ID=@DeleteID
 
								END");

			migrationBuilder.Sql(@"CREATE PROCEDURE [dbo].[MergeStructures]
										-- Add the parameters for the stored procedure here
										@KeepStructureID bigint,
										@MergeStructureID bigint
									AS
									BEGIN
										-- SET NOCOUNT ON added to prevent extra result sets from
										-- interfering with SELECT statements.
										SET NOCOUNT ON;

										declare @MergeNotes nvarchar(max)
										set @MergeNotes = (select notes from Structure where ID = @MergeStructureID)

										update Location 
										set ParentID = @KeepStructureID 
										where ParentID = @MergeStructureID

										update Structure
										set ParentID = @KeepStructureID 
										where ParentID = @MergeStructureID

										IF NOT (@MergeNotes IS NULL OR @MergeNotes = '')
										BEGIN
											declare @crlf nvarchar(2)
											set @crlf = CHAR(13) + CHAR(10)

											declare @MergeHeader nvarchar(80)
											declare @MergeFooter nvarchar(80)
											set @MergeHeader = '*****BEGIN MERGE FROM ' + CONVERT(nvarchar(80), @MergeStructureID) + '*****'
											set @MergeFooter = '*****END MERGE FROM ' + CONVERT(nvarchar(80), @MergeStructureID) + '*****'

											update Structure
											set Notes = Notes + @crlf + @MergeHeader + @crlf + @MergeNotes + @crlf + @MergeFooter + @crlf
											where ID = @KeepStructureID
										END

										-- Delete any structure links directly between the keep and merge structures, a rare occurrence from incorrect annotations
										delete StructureLink where SourceID = @KeepStructureID AND TargetID = @MergeStructureID
										delete StructureLink where TargetID = @KeepStructureID AND SourceID = @MergeStructureID

										update StructureLink
										set TargetID = @KeepStructureID
										where TargetID = @MergeStructureID
		
										update StructureLink
										set SourceID = @KeepStructureID
										where SourceID = @MergeStructureID

										update Structure
										set Notes = 'Merged into structure ' + CONVERT(nvarchar(80), @KeepStructureID)
										where ID = @MergeStructureID

										delete Structure
										where ID = @MergeStructureID
									END");
			
			

			migrationBuilder.Sql(@"CREATE PROCEDURE [dbo].SelectNetworkStructureIDs
										-- Add the parameters for the stored procedure here
										@IDs integer_list READONLY,
										@Hops int
									AS
									BEGIN
										select N.ID as ID from NetworkStructureIDs(@IDs, @Hops) N
									END");

			migrationBuilder.Sql(@"CREATE PROCEDURE [dbo].SelectNetworkChildStructureIDs
										-- Add the parameters for the stored procedure here
										@IDs integer_list READONLY,
										@Hops int
									AS
									BEGIN
										select N.ID as ID from NetworkChildStructureIDs(@IDs, @Hops) N
									END");

			migrationBuilder.Sql(@"CREATE PROCEDURE [dbo].[SelectSectionAnnotationsInVolumeBounds]
										-- Add the parameters for the stored procedure here
										@Z float,
										@BBox geometry,
										@MinRadius float,
										@QueryDate datetime
									AS
									BEGIN
										-- SET NOCOUNT ON added to prevent extra result sets from
										-- interfering with SELECT statements.
										SET NOCOUNT ON;

										DECLARE @LocationsInBounds [dbo].[udtParentChildIDMap]
										DECLARE @ModifiedStructuresInBounds integer_list
										DECLARE @SectionStructureIDsInBounds integer_list
										DECLARE @ModifiedLocationsInBounds integer_list
				 
										--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

										INSERT into @LocationsInBounds (ParentID, ID) SELECT L.ParentID, L.ID FROM Location L
											WHERE Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID

										INSERT INTO @SectionStructureIDsInBounds (ID) 
											select distinct L.ParentID as ID from @LocationsInBounds L
								 
										IF @QueryDate IS NOT NULL
											BEGIN
												--Grab all structures who have had a link or location in the region updated. 
												--This ensures each location in the region has a structure
												INSERT INTO @ModifiedStructuresInBounds (ID) 
	  											  select SIB.ID from (
													select S.ID as ID from Structure S
														inner join @SectionStructureIDsInBounds SIB ON SIB.ID  = S.ID
															where S.LastModified >= @QueryDate
													union
													select S.ID as ID from @SectionStructureIDsInBounds S
														inner join StructureLink SLS ON SLS.SourceID = S.ID
														where SLS.LastModified >= @QueryDate
													union 
													select S.ID as ID from @SectionStructureIDsInBounds S
														inner join StructureLink SLT ON SLT.TargetID = S.ID
														where SLT.LastModified >= @QueryDate ) SIB


												select S.* from Structure S
													inner join @ModifiedStructuresInBounds Modified ON Modified.ID = S.ID

												Select * from StructureLink L
													where (L.TargetID in (Select ID from @ModifiedStructuresInBounds))
														OR (L.SourceID in (Select ID from @ModifiedStructuresInBounds)) 

												INSERT INTO @ModifiedLocationsInBounds (ID)
  												  select ML.ID from (
													select L.ID from @LocationsInBounds LIB
														inner join Location L ON L.ID = LIB.ID
														where L.LastModified >= @QueryDate
													UNION
													select L.ID from @LocationsInBounds L
														inner join LocationLink LL ON LL.A = L.ID
															where LL.Created >= @QueryDate
													UNION
													select L.ID from @LocationsInBounds L
														inner join LocationLink LL ON LL.B = L.ID
															where LL.Created >= @QueryDate
												) ML

												Select L.* from Location L	
													inner join @ModifiedLocationsInBounds MLIB ON MLIB.ID = L.ID 

												Select * from LocationLink
													WHERE ((A in (select ID from @ModifiedLocationsInBounds))
														OR	
														   (B in (select ID from @ModifiedLocationsInBounds)))
								    
											END
										ELSE
											BEGIN
												select S.* from Structure S
													inner join @SectionStructureIDsInBounds SIB ON SIB.ID = S.ID

												Select * from StructureLink L
													where (L.TargetID in (Select ID from @SectionStructureIDsInBounds))
														OR (L.SourceID in (Select ID from @SectionStructureIDsInBounds)) 

												Select L.* from Location L 
													inner join @LocationsInBounds LIB ON LIB.ID = L.ID

												Select * from LocationLink
													WHERE ((A in (select ID from @LocationsInBounds))
														OR	
														   (B in (select ID from @LocationsInBounds)))
											END
	  
									END");

			migrationBuilder.Sql(@"CREATE PROCEDURE [dbo].[SelectSectionAnnotationsInMosaicBounds]
										-- Add the parameters for the stored procedure here
										@Z float,
										@BBox geometry,
										@MinRadius float,
										@QueryDate datetime
									AS
									BEGIN
										-- SET NOCOUNT ON added to prevent extra result sets from
										-- interfering with SELECT statements.
										SET NOCOUNT ON;

										DECLARE @LocationsInBounds [dbo].[udtParentChildIDMap]
										DECLARE @ModifiedStructuresInBounds integer_list
										DECLARE @SectionStructureIDsInBounds integer_list
										DECLARE @ModifiedLocationsInBounds integer_list
				 
										--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

										INSERT into @LocationsInBounds (ParentID, ID) SELECT L.ParentID, L.ID FROM Location L
											WHERE Z = @Z AND (@BBox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID

										INSERT INTO @SectionStructureIDsInBounds (ID) 
											select distinct L.ParentID as ID from @LocationsInBounds L
								 
										IF @QueryDate IS NOT NULL
											BEGIN
												--Grab all structures who have had a link or location in the region updated. 
												--This ensures each location in the region has a structure
												INSERT INTO @ModifiedStructuresInBounds (ID) 
	  											  select SIB.ID from (
													select S.ID as ID from Structure S
														inner join @SectionStructureIDsInBounds SIB ON SIB.ID  = S.ID
															where S.LastModified >= @QueryDate
													union
													select S.ID as ID from @SectionStructureIDsInBounds S
														inner join StructureLink SLS ON SLS.SourceID = S.ID
														where SLS.LastModified >= @QueryDate
													union 
													select S.ID as ID from @SectionStructureIDsInBounds S
														inner join StructureLink SLT ON SLT.TargetID = S.ID
														where SLT.LastModified >= @QueryDate ) SIB


												select S.* from Structure S
													inner join @ModifiedStructuresInBounds Modified ON Modified.ID = S.ID

												Select * from StructureLink L
													where (L.TargetID in (Select ID from @ModifiedStructuresInBounds))
														OR (L.SourceID in (Select ID from @ModifiedStructuresInBounds)) 

												INSERT INTO @ModifiedLocationsInBounds (ID)
  												  select ML.ID from (
													select L.ID from @LocationsInBounds LIB
														inner join Location L ON L.ID = LIB.ID
														where L.LastModified >= @QueryDate
													UNION
													select L.ID from @LocationsInBounds L
														inner join LocationLink LL ON LL.A = L.ID
															where LL.Created >= @QueryDate
													UNION
													select L.ID from @LocationsInBounds L
														inner join LocationLink LL ON LL.B = L.ID
															where LL.Created >= @QueryDate
												) ML

												Select L.* from Location L	
													inner join @ModifiedLocationsInBounds MLIB ON MLIB.ID = L.ID 

												Select * from LocationLink
													WHERE ((A in (select ID from @ModifiedLocationsInBounds))
														OR	
														   (B in (select ID from @ModifiedLocationsInBounds)))
								    
											END
										ELSE
											BEGIN
												select S.* from Structure S
													inner join @SectionStructureIDsInBounds SIB ON SIB.ID = S.ID

												Select * from StructureLink L
													where (L.TargetID in (Select ID from @SectionStructureIDsInBounds))
														OR (L.SourceID in (Select ID from @SectionStructureIDsInBounds)) 

												Select L.* from Location L 
													inner join @LocationsInBounds LIB ON LIB.ID = L.ID

												Select * from LocationLink
													WHERE ((A in (select ID from @LocationsInBounds))
														OR	
														   (B in (select ID from @LocationsInBounds)))
											END
	  
									END");

			migrationBuilder.Sql(@"CREATE PROCEDURE SplitStructure
										-- Add the parameters for the stored procedure here
										@LocationIDOfSplitStructure bigint,
										@SplitStructureID bigint OUTPUT
									AS
									BEGIN
										-- SET NOCOUNT ON added to prevent extra result sets from
										-- interfering with SELECT statements.
										SET NOCOUNT ON;

										IF OBJECT_ID('tempdb..#LocationLinkPool') IS NOT NULL DROP TABLE #LocationLinkPool
										IF OBJECT_ID('tempdb..#LocationsInKeepSubGraph') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph
										IF OBJECT_ID('tempdb..#LocationsInSplitSubGraph') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph
										IF OBJECT_ID('tempdb..#ChildStructureLocations') IS NOT NULL DROP TABLE #ChildStructureLocations
										IF OBJECT_ID('tempdb..#StructureLocations') IS NOT NULL DROP TABLE #StructureLocations
										IF OBJECT_ID('tempdb..#DistanceToEachStructure') IS NOT NULL DROP TABLE #DistanceToEachStructure
										IF OBJECT_ID('tempdb..#DistanceToNearestStructure') IS NOT NULL DROP TABLE #DistanceToNearestStructure
										IF OBJECT_ID('tempdb..#ParentIDForChildStructure') IS NOT NULL DROP TABLE #ParentIDForChildStructure

										set @SplitStructureID = 0 
										DECLARE @KeepStructureID bigint 

										set @KeepStructureID = (select ParentID from Location where ID = @LocationIDOfSplitStructure)
	
										SELECT A,B into #LocationLinkPool from dbo.StructureLocationLinks(@KeepStructureID) order by A

										--select * from #LocationLinkPool where A = @LocationIDOfSplitStructure OR B = @LocationIDOfSplitStructure

										CREATE TABLE #LocationsInSplitSubGraph(ID bigint)
										insert into #LocationsInSplitSubGraph (ID) values (@LocationIDOfSplitStructure)
	  
										--Loop over the pool adding to the subgraph until we cannot find any more locations
										DECLARE @RowsAddedToSubgraph bigint
										set @RowsAddedToSubgraph = 1
										While @RowsAddedToSubgraph > 0
										BEGIN
										--insert into #GAggregate (SParentID, Shape) Select SParentID, TMosaicShape from #StructureLinks where TMosaicShape is NOT NULL

											insert into #LocationsInSplitSubGraph (ID) 
												Select B as ID from #LocationLinkPool where A in (select ID from #LocationsInSplitSubGraph)
												union 
												Select A as ID from #LocationLinkPool where B in (select ID from #LocationsInSplitSubGraph)

											set @RowsAddedToSubgraph = @@ROWCOUNT

											--select distinct(ID) from #LocationsInSplitSubGraph

											--Remove links we have already added
											delete LLP from #LocationLinkPool LLP
											join #LocationsInSplitSubGraph SA ON SA.ID = LLP.A
											join #LocationsInSplitSubGraph SB ON SB.ID = LLP.B
										END

										select ID into #LocationsInKeepSubGraph from Location where ParentID = @KeepStructureID AND ID not in (select ID from #LocationsInSplitSubGraph)

										IF ((select COUNT(ID) from #LocationsInKeepSubGraph) = 0)
											THROW 50000, N'The split structure is connected to the entire keep cell.  Break a location link to create two subgraphs and try again', 1;

										--We have built the list of annotations to be used for the old and new structure.  Create a new structure for the split and capture the ID
										INSERT INTO Structure (TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified)
											SELECT TypeID, Notes, Verified, Tags, Confidence, ParentID, Created, Label, Username, LastModified from Structure S where
												S.ID = @KeepStructureID
										set @SplitStructureID = SCOPE_IDENTITY()

										select VolumeShape, Z, KL.ID, @KeepStructureID as ParentID into  #StructureLocations
										FROM Location L 
										JOIN #LocationsInKeepSubGraph KL ON KL.ID = L.ID
										UNION ALL
										select VolumeShape, Z, SL.ID, @SplitStructureID as ParentID FROM Location L 
										JOIN #LocationsInSplitSubGraph SL ON SL.ID = L.ID

										select ParentID as StructureID, geometry::ConvexHullAggregate(VolumeShape) as Shape, AVG(Z) as Z 
											into #ChildStructureLocations from Location
											where ParentID in (select ID from Structure where ParentID = @KeepStructureID)
											group by ParentID

										--Find the nearest location in either the keep or split structure
										select CSL.StructureID as StructureID, SL.ParentID as NewParentID, MIN(SL.VolumeShape.STDistance(CSL.Shape)) as Distance
											INTO #DistanceToEachStructure from #ChildStructureLocations CSL
											join #StructureLocations SL ON SL.Z = CSL.Z
											Group By CSL.StructureID, SL.ParentID 
											order by CSL.StructureID

										select SL.StructureID as StructureID, MIN(SL.Distance) as Distance 
										INTO #DistanceToNearestStructure from #DistanceToEachStructure SL
										group by SL.StructureID 

										select SD.StructureID as StructureID, SD.NewParentID as NewParentID, SD.Distance as Distance
										into #ParentIDForChildStructure from #DistanceToEachStructure SD
										join #DistanceToNearestStructure SN ON SN.StructureID = SD.StructureID AND SN.Distance = SD.Distance

										update Location set ParentID = @SplitStructureID
										FROM Location L
											INNER JOIN #LocationsInSplitSubGraph LS ON LS.ID = L.ID

										update Structure set ParentID = PCS.NewParentID 
										FROM Structure S
											JOIN #ParentIDForChildStructure PCS ON S.ID = PCS.StructureID

										IF OBJECT_ID('tempdb..#LocationLinkPool') IS NOT NULL DROP TABLE #LocationLinkPool
										IF OBJECT_ID('tempdb..#LocationsInKeepSubGraph') IS NOT NULL DROP TABLE #LocationsInKeepSubGraph
										IF OBJECT_ID('tempdb..#LocationsInSplitSubGraph') IS NOT NULL DROP TABLE #LocationsInSplitSubGraph
										IF OBJECT_ID('tempdb..#ChildStructureLocations') IS NOT NULL DROP TABLE #ChildStructureLocations
										IF OBJECT_ID('tempdb..#StructureLocations') IS NOT NULL DROP TABLE #StructureLocations
										IF OBJECT_ID('tempdb..#DistanceToEachStructure') IS NOT NULL DROP TABLE #DistanceToEachStructure
										IF OBJECT_ID('tempdb..#DistanceToNearestStructure') IS NOT NULL DROP TABLE #DistanceToNearestStructure
										IF OBJECT_ID('tempdb..#ParentIDForChildStructure') IS NOT NULL DROP TABLE #ParentIDForChildStructure 

										RETURN 0
										END");

			migrationBuilder.Sql(@"CREATE PROCEDURE SplitStructureAtLocationLink
										@LocationIDOfKeepStructure bigint,
										@LocationIDOfSplitStructure bigint,
										@SplitStructureID bigint OUTPUT
									AS
									BEGIN
										-- SET NOCOUNT ON added to prevent extra result sets from
										-- interfering with SELECT statements.
										SET NOCOUNT ON;
	
										set @SplitStructureID = 0

										--Ensure that the location IDs of the keep and split locations are a location link.  Remove the link and continue;
										IF (0 = (select COUNT(A) from LocationLink where (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure) OR 
																				  (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure)))
											THROW 50000, N'The Split and Keep Location IDs must be linked', 1;

										BEGIN TRANSACTION split

											Delete LocationLink where (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure) OR 
																		   (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure)
											Exec SplitStructure @LocationIDOfSplitStructure, @SplitStructureID

											if(@@error <> 0)
											 begin
												ROLLBACK TRANSACTION 
												RETURN
											 end 

										COMMIT TRANSACTION split

									END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
			migrationBuilder.Sql(@"DROP INDEX MosaicShape_Index");
			migrationBuilder.Sql(@"DROP INDEX VolumeShape_Index");

			migrationBuilder.Sql(@"DROP TRIGGER Location_update");
			migrationBuilder.Sql(@"DROP TRIGGER Location_delete");
			migrationBuilder.Sql(@"DROP TRIGGER StructureLink_ReciprocalCheck");
			migrationBuilder.Sql(@"DROP TRIGGER StructureType_LastModified");
			migrationBuilder.Sql(@"DROP TRIGGER Structure_LastModified");

			migrationBuilder.Sql(@"DROP TYPE [dbo].[integer_list]");
            migrationBuilder.Sql(@"DROP TYPE [dbo].[udtParentChildIDMap]");
            migrationBuilder.Sql(@"DROP TYPE [dbo].[udtLinks]");

            migrationBuilder.Sql(
                    $" ALTER DATABASE CURRENT DROP FILEGROUP {MemOptimizedFilegroup}",
                    suppressTransaction: true);
        }
    }
}
