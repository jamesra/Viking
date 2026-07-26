
			CREATE PROCEDURE [dbo].[SelectSectionStructures]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN 
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations
					select * into #SectionLocationsInBounds from Location where Z = @Z order by ParentID

					IF @QueryDate IS NOT NULL
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocations) AND LastModified >= @QueryDate
					ELSE
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocations)
			END

			