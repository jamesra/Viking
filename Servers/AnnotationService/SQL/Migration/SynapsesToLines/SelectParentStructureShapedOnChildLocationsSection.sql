declare @LocationID bigint
set @LocationID = 22118

select L.ID as LocationID, S.ID as StructureID, S.Label, MosaicShape, L.Z as Z,
	L.MosaicShape.STDistance( (select MosaicShape from Location where ID = @LocationID)) as Distance
	from Location L 
	INNER JOIN Structure S ON S.ID = L.ParentID --Merge with the parent structure
	where L.Z = (select Z from Location where ID = @LocationID) AND
		 L.ParentID = (select PS.ID from Structure PS
					INNER JOIN Structure CS ON CS.ParentID = PS.ID 
					where 
					CS.ID = (Select ParentID from Location where ID = @LocationID)
					)
			--Select the ID of the parent structure for this location--
			
--TODO, rewrite as a function or make it a view for all locations
declare @LocationID bigint
set @LocationID = 22118		
declare @LocMosaicShape geometry
set @LocMosaicShape = (select MosaicShape from Location where ID = @LocationID)

select L.ID as LocationID, MosaicShape, S.ID, S.Label, L.Z as Z
	from Location L 
	INNER JOIN Structure S ON S.ID = L.ParentID
	INNER JOIN(
			--Determine the minimum possible distance to an annotation on the structure's parent structure
			select L.ParentID as StructureID, MIN(L.MosaicShape.STDistance(@LocMosaicShape)) as Distance
			from Location L 
			where L.Z = (select Z from Location where ID = @LocationID) AND
				 L.ParentID = (select PS.ID from Structure PS
							INNER JOIN Structure CS ON CS.ParentID = PS.ID 
							where 
							CS.ID = (Select ParentID from Location where ID = @LocationID)
							) 
			group by L.ParentID
			) LocationDistance
		ON LocationDistance.StructureID = L.ParentID

		WHERE LocationDistance.StructureID = L.ParentID AND L.MosaicShape.STDistance(@LocMosaicShape) <= LocationDistance.Distance


--Create all combinations of child structure locations and parent structure locations on a section
select L.ParentID as ChildStructureID, L.ID as ChildLocationID, L.Z, PL.ParentID as ParentStructureID, PL.ID as ParentLocationID from Location L
	INNER JOIN Structure S ON S.ID = L.ParentID
	INNER JOIN Structure PS ON S.ParentID = PS.ID
	INNER JOIN Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	order by ParentStructureID, ChildStructureID, ChildLocationID, ParentLocationID, Z

--Create all combinations of child structure locations and parent structure locations on a section
select L.ParentID as ChildStructureID, L.ID as ChildLocationID, L.Z, 
	   PL.ParentID as ParentStructureID, PL.ID as ParentLocationID,
	   MIN(L.MosaicShape.STDistance(PL.MosaicShape)) as Distance
	from Location L
	INNER JOIN Structure S ON S.ID = L.ParentID
	INNER JOIN Structure PS ON S.ParentID = PS.ID
	INNER JOIN Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	GROUP BY L.ID, L.ParentID, PL.ParentID, PL.ID, L.Z 


select L.ParentID as ChildStructureID, L.ID as ChildLocationID, L.Z, 
	   PL.ParentID as ParentStructureID, PL.ID as ParentLocationID,
	   L.MosaicShape.STDistance(PL.MosaicShape) as Distance
	from Location L
	
	INNER JOIN Structure S ON S.ID = L.ParentID
	INNER JOIN Structure PS ON S.ParentID = PS.ID
	INNER JOIN Location PL ON PL.ParentID = PS.ID AND PL.Z = L.Z
	INNER JOIN dbo.vParentStructureLocationDistance D ON D.ChildLocationID = L.ID
	WHERE L.MosaicShape.STDistance(PL.MosaicShape) = MinDistance
	order by ChildStructureID, ChildLocationID