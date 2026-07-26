
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinksInVolumeBounds]
			-- Add the parameters for the stored procedure here
			@Z float,
			@BBox geometry,
			@MinRadius float,
			@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID('tempdb..#SectionIDsInBounds') IS NOT NULL DROP TABLE #SectionIDsInBounds
					IF OBJECT_ID('tempdb..#AllSectionStructuresInBounds') IS NOT NULL DROP TABLE #AllSectionStructuresInBounds
					select S.* into #SectionStructuresInBounds from Structure S
						inner join (Select distinct ParentID from Location where (@bbox.STIntersects(VolumeShape) = 1 and Z = @Z AND Radius >= @MinRadius)) L ON L.ParentID = S.ID
						
					IF @QueryDate IS NOT NULL
						BEGIN
							select SIB.ID into #SectionIDsInBounds from (
								select S.ID as ID from #SectionStructuresInBounds S
									where S.LastModified >= @QueryDate
								union
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLS ON SLS.SourceID = S.ID
									where SLS.LastModified >= @QueryDate
								union 
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLT ON SLT.TargetID = S.ID
									where SLT.LastModified >= @QueryDate ) SIB

							select S.* from #SectionStructuresInBounds S
								inner join #SectionIDsInBounds Modified ON Modified.ID = S.ID

							Select * from StructureLink L
								where (L.TargetID in (Select ID from #SectionIDsInBounds))
									OR (L.SourceID in (Select ID from #SectionIDsInBounds)) 

							DROP TABLE #SectionIDsInBounds
						END
					ELSE
						BEGIN
							select * from #SectionStructuresInBounds

							Select * from StructureLink L
							where (L.TargetID in (Select ID from #SectionStructuresInBounds))
								OR (L.SourceID in (Select ID from #SectionStructuresInBounds)) 
						END
			  
					DROP TABLE #SectionStructuresInBounds
			END 
		