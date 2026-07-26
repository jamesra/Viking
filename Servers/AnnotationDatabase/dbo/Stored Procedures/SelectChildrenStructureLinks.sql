CREATE PROCEDURE [dbo].[SelectChildrenStructureLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				select * from StructureLink where 
				SourceID in (Select ID from Structure where ParentID = @StructureID) 
				or
				TargetID in (Select ID from Structure where ParentID = @StructureID)
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectChildrenStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectChildrenStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];

