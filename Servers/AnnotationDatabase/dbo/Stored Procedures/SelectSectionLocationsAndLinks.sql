
			CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations
				select * into #SectionLocations from Location where Z = @Z ORDER BY ID
	
				IF @QueryDate IS NOT NULL
					Select * from #SectionLocations
					where LastModified >= @QueryDate
				ELSE
					Select * from #SectionLocations
		
				IF @QueryDate IS NOT NULL
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(SELECT ID
					  from #SectionLocations)
					 )
					  OR
					  (B in 
					(SELECT ID
					  from #SectionLocations)
					 ))
					 AND Created >= @QueryDate
				ELSE
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(SELECT ID
					  from #SectionLocations)
					 )
					  OR
					  (B in 
					(SELECT ID
					  from #SectionLocations)
					 )) 
			END
			