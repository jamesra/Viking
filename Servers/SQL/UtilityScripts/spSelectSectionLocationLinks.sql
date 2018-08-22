
GO
/****** Object:  StoredProcedure [dbo].[SelectSectionLocationLinksAfterDate]    Script Date: 01/12/2011 14:27:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
Create PROCEDURE [dbo].[SelectSectionLocationLinksAfterDate]
	-- Add the parameters for the stored procedure here
	@Z float
	@QueryDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
		
    Select * from LocationLink
	 WHERE (((A in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z >= @Z)
	 )
	  AND
	  (B in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z <= @Z)
	 ))
	 OR
	 ((A in
	 (SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z <= @Z)
	 )
	  AND
	  (B in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z >= @Z)
	 )))
	 AND Created >= @QueryDate
	 
END
