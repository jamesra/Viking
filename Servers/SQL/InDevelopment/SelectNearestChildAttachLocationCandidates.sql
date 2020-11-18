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
ALTER PROCEDURE [dbo].[SelectNearestChildAttachLocationCandidates]
	@ParentStructureIDs integer_list READONLY -- Parent StructureIDs who we want to find children of
AS
BEGIN 
	SET NOCOUNT ON;

    declare @ParentChildPairs [dbo].[udtParentChildLocationPairs]
	declare @AllowedZRange float
	declare @MissingLocIDs integer_list
	set @AllowedZRange = 1

	INSERT INTO @MissingLocIDs select L.ID 
		from @ParentStructureIDs Parent
		inner join Structure Child on Child.ParentID = Parent.ID
		inner join Location L      on L.ParentID = Child.ID
		where EXISTS (SELECT ID from Location PL where PL.ParentID = Parent.ID) --Ensure parent structure has at least one location
		 
	EXEC SelectNearestParentAttachCandidatesForLocations @MissingLocIDs
END
GO
