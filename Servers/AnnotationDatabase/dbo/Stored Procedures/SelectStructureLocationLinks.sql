
			CREATE PROCEDURE [dbo].[SelectStructureLocationLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				Select LL.* from LocationLink LL
					 join Location L ON L.ID = A
					 where L.ParentID = @StructureID
	 
			END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectStructureLocationLinks] TO PUBLIC
    AS [dbo];

