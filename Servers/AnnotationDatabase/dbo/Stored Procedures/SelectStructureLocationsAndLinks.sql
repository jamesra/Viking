
			Create PROCEDURE [dbo].[SelectStructureLocationsAndLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				select * from Location where ParentID = @StructureID 

				IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations
				select ID into #SectionLocations from Location where ParentID = @StructureID ORDER BY ID
	
				-- Insert statements for procedure here
				Select ll.* from LocationLink ll JOIN #SectionLocations sl ON (sl.ID = ll.A)
				UNION
				Select ll.* from LocationLink ll JOIN #SectionLocations sl ON (sl.ID = ll.B)
			END
		