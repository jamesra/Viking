
				CREATE PROCEDURE [dbo].[SelectSectionLocationLinksInMosaicBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@bbox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				--This really needs to check if a line between the two location links intersects the bounding box.

				IF OBJECT_ID('tempdb..#LocationsAboveZ') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID('tempdb..#LocationsBelowZ') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z AND (@bbox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID 
				select * into #LocationsBelowZ from Location where Z <= @Z AND (@bbox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID

	
				IF @QueryDate IS NOT NULL
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )))
					 AND Created >= @QueryDate
				ELSE
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 ))) 

				DROP TABLE #LocationsAboveZ
				DROP TABLE #LocationsBelowZ
	 
			END
			