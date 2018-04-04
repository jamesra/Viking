-- ================================================
-- Template generated from Template Explorer using:
-- Create Procedure (New Menu).SQL
--
-- Use the Specify Values for Template Parameters 
-- command (Ctrl-Shift-M) to fill in the parameter 
-- values below.
--
-- This block of comments will not be included in
-- the definition of the procedure.
-- ================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE DeepDeleteStructure
	-- Add the parameters for the stored procedure here
	@DeleteID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	if OBJECT_ID('tempdb..#StructuresToDelete') is not null
		DROP Table #StructuresToDelete

	select ID into #StructuresToDelete from (Select ID from Structure where ID = @DeleteID or ParentID = @DeleteID) as ID

	delete from LocationLink
	where A in 
	(
	Select ID from Location 
	where ParentID in (Select ID From #StructuresToDelete) ) 

	delete from LocationLink
	where B in 
	(
	Select ID from Location where ParentID in (Select ID From #StructuresToDelete) ) 

	delete from Location
	where ParentID in (Select ID From #StructuresToDelete)

	delete from StructureLink where SourceID in (Select ID From #StructuresToDelete) or TargetID in (Select ID From #StructuresToDelete)

	delete from Structure
	where ParentID=@DeleteID

	delete from Structure
	where ID=@DeleteID

	if OBJECT_ID('tempdb..#StructuresToDelete') is not null
		DROP Table #StructuresToDelete
END
GO
