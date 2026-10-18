-- UpdateStructureType: sets Structure.TypeID = @TypeID for the structure @StructureID.
-- @StructureID: Structure.ID to change. @TypeID: StructureType.ID to assign.
-- Callers: none in this repo besides the generated EF wrappers (ConnectomeEntities.UpdateStructureType,
-- AnnotationContextProcedures.UpdateStructureTypeAsync). WCF UpdateStructureTypes edits StructureType rows instead.

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

