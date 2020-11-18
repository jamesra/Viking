
BEGIN
	declare @ParentChildPairs udtParentChildLocationPairs
	declare @AllowedZRange float
	set @AllowedZRange = 1 --Include locations that are this distance away from parent annotations on the same section

	--Start by finding shapes on the same Z level +/-  as the origin location
	insert into @ParentChildPairs (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
		select L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
		 
		from Location L
		inner join Structure Child on Child.ID = L.ParentID 
		inner join Location PL on PL.ParentID = Child.ParentID
		where Child.ParentID is not NULL
			  AND ABS(L.Z - PL.Z) <= @AllowedZRange  

	--select * from #ChildLocParentLocPairingCandidates

	Declare @MissingLocIDs integer_list
	INSERT INTO @MissingLocIDs select L.ID 
			from Location L
			inner join Structure S on S.ID = L.ParentID
			where S.ParentID is not NULL AND 
				  L.ID NOT IN (Select ChildLocationID from @ParentChildPairs) AND
				  EXISTS (SELECT ID from Location PL where PL.ParentID = S.ID) --Ensure parent structure has at least one location

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

--The version above adds this row to the results compared to the original loop version
--ChildLocationID ChildStructureID ParentLocationID ParentStructureID
--1260569		  64823			   134034			458
