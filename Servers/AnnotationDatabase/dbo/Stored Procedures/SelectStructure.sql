
		CREATE PROCEDURE [dbo].[SelectStructure]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			-- Insert statements for procedure here
			SELECT [ID]
			   ,[ParentID]
			   ,[TypeID]
			   ,[Label]
			   ,[LastModified]
			  FROM [Rabbit].[dbo].Structure S
			  WHERE ID = @StructureID OR ParentID = @StructureID
			  ORDER BY ID
		END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectStructure] TO PUBLIC
    AS [dbo];

