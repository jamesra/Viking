
		CREATE PROCEDURE [dbo].[SelectStructureLocations]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			-- Insert statements for procedure here
			SELECT L.ID
			   ,[ParentID]
			  ,[VolumeX]
			  ,[VolumeY]
			  ,[Z]
			  ,[Radius]
			  ,[X]
			  ,[Y]
			  FROM [Rabbit].[dbo].[Location] L
			  INNER JOIN 
			   (SELECT ID, TYPEID
				FROM Structure
				WHERE ID = @StructureID OR ParentID = @StructureID) J
			  ON L.ParentID = J.ID
			  ORDER BY ID
		END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectStructureLocations] TO PUBLIC
    AS [dbo];

