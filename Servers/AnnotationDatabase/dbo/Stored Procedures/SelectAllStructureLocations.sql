
		 --add the column, or whatever else you may need to do
		 CREATE PROCEDURE [dbo].[SelectAllStructureLocations]
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
				  FROM  [Location] L
				  ORDER BY ID
			END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectAllStructureLocations] TO PUBLIC
    AS [dbo];

