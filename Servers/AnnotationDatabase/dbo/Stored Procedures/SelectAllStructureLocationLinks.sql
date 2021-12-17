-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE SelectAllStructureLocationLinks
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	Select * from LocationLink	 
END

GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectAllStructureLocationLinks] TO PUBLIC
    AS [dbo];

