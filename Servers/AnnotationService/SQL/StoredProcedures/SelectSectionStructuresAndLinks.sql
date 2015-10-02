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
CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinks]
	-- Add the parameters for the stored procedure here
	@Z float,
	@QueryDate datetime
AS
BEGIN 
		IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations
		IF OBJECT_ID('tempdb..#SectionStructures') IS NOT NULL DROP TABLE #SectionStructures
		select * into #SectionLocations from Location where Z = @Z order by ParentID
		select * into #SectionStructures from Structure where ID in (
				select distinct ParentID from #SectionLocations)

		IF @QueryDate IS NOT NULL
			select * from #SectionStructures where LastModified >= @QueryDate
		ELSE
			select * from #SectionStructures

		Select * from StructureLink L
		where (L.TargetID in (Select ID from #SectionStructures))
			OR (L.SourceID in (Select ID from #SectionStructures)) 
END
