-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[SelectNearestParentAttachLocationCandidates]
	@ChildStructureIDs integer_list READONLY -- Parent StructureIDs who we want to find children of
AS
BEGIN 
	SET NOCOUNT ON;

    declare @ParentChildPairs [dbo].[udtParentChildLocationPairs]
	declare @AllowedZRange float
	declare @MissingLocIDs integer_list
	set @AllowedZRange = 1

	INSERT INTO @MissingLocIDs select L.ID 
		from @ChildStructureIDs ChildID
		inner join Structure Child  on Child.ID = ChildID.ID
		inner join Structure Parent on Child.ParentID = Parent.ID
		inner join Location L      on L.ParentID = Child.ID
		where EXISTS (SELECT ID from Location PL where PL.ParentID = Parent.ID) --Ensure parent structure has at least one location
		 
	EXEC SelectNearestParentAttachCandidatesForLocations @MissingLocIDs
END
