
			 CREATE PROCEDURE UpdateStructureType
				@StructureID bigint,
				@TypeID bigint
			 AS
			 BEGIN
			 SET NOCOUNT ON;
  
			 UPDATE STRUCTURE SET TypeID=@TypeID WHERE ID = @STRUCTUREID
			 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[UpdateStructureType] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[UpdateStructureType] TO [AnnotationPowerUser]
    AS [dbo];

