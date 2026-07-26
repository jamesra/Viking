
			CREATE PROCEDURE [dbo].[SelectSectionLocationLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID('tempdb..#LocationsAboveZ') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID('tempdb..#LocationsBelowZ') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z order by ID
				select * into #LocationsBelowZ from Location where Z <= @Z order by ID

	
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
	 
			END
			