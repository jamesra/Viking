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
-- Description:	Given a set of child location IDs, returns possible candidates for the nearest location on 
-- the parent structure
-- =============================================
ALTER PROCEDURE SelectNearestParentAttachCandidatesForLocations
	@ChildLocIDs integer_list READONLY -- child location IDs we want to find the attachment candidates for
AS
BEGIN 
	SET NOCOUNT ON;

    declare @ParentChildPairs [dbo].[udtParentChildLocationPairs]
	declare @AllowedZRange float
	
	set @AllowedZRange = 1
	 
	--Start by finding shapes on the same Z level +/-  as the origin location
	insert into @ParentChildPairs (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
		select L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
		 
		from @ChildLocIDs Missing
		inner join Location L      on L.ID        = Missing.ID
		inner join Structure Child on Child.ID    = L.ParentID 
		inner join Location PL     on PL.ParentID = Child.ParentID
		where Child.ParentID is not NULL
			  AND ABS(L.Z - PL.Z) <= @AllowedZRange

	--select * from #ChildLocParentLocPairingCandidates

	declare @MissingLocIDs integer_list
	INSERT INTO @MissingLocIDs select ChLoc.ID 
			from @ChildLocIDs ChLoc
			right join @ParentChildPairs Ca on Ca.ChildLocationID = ChLoc.ID
			--inner join Structure S	on S.ID = L.ParentID
			where Ca.ChildLocationID IS NULL --AND
			      --S.ParentID is not NULL AND 
				  --L.ID NOT IN (Select ChildLocationID from @ParentChildPairs) AND
				  --EXISTS (SELECT ID from Location PL where PL.ParentID = S.ID) --Ensure parent structure has at least one location

    --select ID from @MissingLocIDs
	--select COUNT(ID) as NumRemaining from @MissingLocIDs 

	DECLARE @NearestLocationZ TABLE
	(
		ID bigint , --Location ID,
		ZDelta bigint  --Distance in Z

		PRIMARY KEY(
		ID
		)
	)
	
	insert into @NearestLocationZ
		select Missing.ID as ID, MIN(ABS(L.Z - PL.Z)) as ZDelta 
		from @MissingLocIDs Missing
		  inner join Location L ON Missing.ID = L.ID
		  inner join Structure Child on Child.ID = L.ParentID 
		  inner join Location PL on PL.ParentID = Child.ParentID
		group by Missing.ID
  
	insert into @ParentChildPairs (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
		SELECT L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			--MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
			from @MissingLocIDs Missing
			inner join Location L on L.ID = Missing.ID
			inner join Structure Child on Child.ID = L.ParentID 
			inner join Location PL on PL.ParentID = Child.ParentID
			inner join @NearestLocationZ NearestL on NearestL.ID = Missing.ID
			where	Child.ParentID is not NULL 
					AND ABS(L.Z - PL.Z) <= NearestL.ZDelta
		 
	select * from @ParentChildPairs
END
GO
