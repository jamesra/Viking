
GO
/****** Object:  StoredProcedure [dbo].[SelectSectionLocationsAndLinksAfterDate]    Script Date: 01/12/2011 14:27:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinksAfterDate]
	-- Add the parameters for the stored procedure here
	@Z float,
	@QueryDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	Select * from Location 
	where Z = @Z AND LastModified >= @QueryDate
	
    -- Insert statements for procedure here
	Select * from LocationLink
	 WHERE Created >= @QueryDate AND ((A in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z = @Z)
	 )
	  OR
	  (B in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z = @Z)
	 ))
	 
END
