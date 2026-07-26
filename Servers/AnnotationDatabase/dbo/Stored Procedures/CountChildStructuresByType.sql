
			 CREATE PROCEDURE [dbo].[CountChildStructuresByType]
				@StructureID bigint
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select TypeID, COUNT(TypeID) as Count from Structure 
				where ParentID = @StructureID
				group by TypeID
			 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[CountChildStructuresByType] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[CountChildStructuresByType] TO [AnnotationPowerUser]
    AS [dbo];

