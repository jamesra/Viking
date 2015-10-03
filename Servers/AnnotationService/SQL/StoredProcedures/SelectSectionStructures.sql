USE [Test]
GO
/****** Object:  StoredProcedure [dbo].[SelectSectionStructuresInBounds]    Script Date: 9/15/2015 4:19:32 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[SelectSectionStructures]
	-- Add the parameters for the stored procedure here
	@Z float,
	@QueryDate datetime
AS
BEGIN 
		IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations
		select * into #SectionLocationsInBounds from Location where Z = @Z order by ParentID

		IF @QueryDate IS NOT NULL
			select * from Structure where ID in (
				select distinct ParentID from #SectionLocationsInBounds) AND LastModified >= @QueryDate
		ELSE
			select * from Structure where ID in (
				select distinct ParentID from #SectionLocationsInBounds)
END
