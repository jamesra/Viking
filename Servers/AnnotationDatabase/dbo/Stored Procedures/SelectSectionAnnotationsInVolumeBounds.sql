CREATE PROCEDURE [dbo].[SelectSectionAnnotationsInVolumeBounds]
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
	  
			END