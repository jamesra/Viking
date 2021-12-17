CREATE PROCEDURE [dbo].[SelectAllStructures]
		-- Add the parameters for the stored procedure here
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
			  FROM [dbo].Structure S
			  ORDER BY ID
		END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectAllStructures] TO PUBLIC
    AS [dbo];

