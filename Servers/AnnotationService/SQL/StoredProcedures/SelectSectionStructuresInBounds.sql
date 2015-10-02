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
ALTER PROCEDURE [dbo].[SelectSectionStructuresInBounds]
	-- Add the parameters for the stored procedure here
	@Z float,
	@BBox geometry,
	@MinRadius float,
	@QueryDate datetime
AS
BEGIN 
		IF OBJECT_ID('tempdb..#SectionLocationsInBounds') IS NOT NULL DROP TABLE #SectionLocationsInBounds
		select * into #SectionLocationsInBounds from Location where (@bbox.STIntersects(VolumeShape) = 1) and Z = @Z AND Radius >= @MinRadius order by ParentID

		select * from Structure where ID in (
			select distinct ParentID from #SectionLocationsInBounds)
END
