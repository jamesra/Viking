/*********************************************
/*Run this block of code if you change any of the first three queries or annotations for the structure.  It will clear the cache.*/

if OBJECT_ID('tempdb..#LocCenters') is not null
	DROP Table #LocCenters

if OBJECT_ID('tempdb..#Connection') is not null
	DROP Table #Connection

if OBJECT_ID('tempdb..#DistancePairs') is not null
	DROP Table #DistancePairs

if OBJECT_ID('tempdb..#MinDistancePair') is not null
	DROP Table #MinDistancePair

if OBJECT_ID('tempdb..#MixedSynapses') is not null
	DROP Table #MixedSynapses

if OBJECT_ID('tempdb..#JoinableMixedSynapses') is not null
	DROP Table #JoinableMixedSynapses

**********************************************/

/*********************

@Crystal question about distance measurements: (others feel free to weigh in) 
I'm building a new table that lists the "Attachment point" for each child structure to its parent.  I want this to be the parent and child locations that have the shortest distance between them.  Computationally it isn't possible to find the actual closest point of approach.  So I start by looking at location pairs on the same section.  Then I grow the search range by 1 until I locate at least one candidate for each child location.

Now for the edge case: I know we have rare cases where a child structure is at the end of a vertically oriented process, and the closest point of approach is +/- in Z.  So to reduce the chance of latching onto a location on another branch of the cell that is far away and missing the adjacent process in Z I start the initial search +/- 1 in Z.  However, this runs the risk of image registration shifts resulting in artificially low distance values.

--AAAA--------    ^
-----AAAAA----    |
--AAAA-B------    |
--AAAA--------    |
--AAAA--------    Z

In the example above, B is a child structure of A.  By searching +/- 1 Z levels the registration error will result in a distance measurement of one section thickness when I could have measured in XY.  

***********************/
DROP TABLE IF EXISTS tempdb.#ChildLocParentLocPairingCandidates

DROP TABLE IF EXISTS tempdb.#ChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#ChildStructureCenters
DROP TABLE IF EXISTS tempdb.#MinChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#ChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#MinChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#ChildAttachLocations

DROP TABLE IF EXISTS tempdb.#ChildStructureEdgeCount
DROP TABLE IF EXISTS tempdb.#ChildStructureCenters
DROP TABLE IF EXISTS tempdb.#ChildAttachLocationsRefinedDistances
DROP TABLE IF EXISTS tempdb.#MinChildAttachLocationsRefinedDistances
DROP TABLE IF EXISTS tempdb.#RefinedChildAttachLocations

print 'Building list of potential child->parent location attachment points'
go

if OBJECT_ID('tempdb..#ChildLocParentLocPairingCandidates') is null
BEGIN
	declare @AllowedZRange float
	set @AllowedZRange = 1

	--Start by finding shapes on the same Z level +/-  as the origin location
	select L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
		into #ChildLocParentLocPairingCandidates
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
				  L.ID NOT IN (Select ChildLocationID from #ChildLocParentLocPairingCandidates) AND
				  0 < (SELECT COUNT(ID) from Location PL where PL.ParentID = S.ID) --Ensure parent structure has at least one location

    --select ID from @MissingLocIDs
	select COUNT(ID) as NumRemaining from @MissingLocIDs 

	----------------------------------------------------------

	--Select * from @MissingLocIDs Missing
	--    inner join Location L ON L.ID = Missing.ID
	--	inner join Structure S on S.ID = L.ParentID
	--	inner join Location PL on PL.ParentID = s.ParentID


	----------------------------------------------------------
	 
	declare @MaxZ float
	set @MaxZ = (Select Max(L.Z) from Location L) 

	select TOP 10 ID from @MissingLocIDs
	set @AllowedZRange = @AllowedZRange + 1

	WHILE (select COUNT(ID) from @MissingLocIDs) > 1 AND @AllowedZRange < @MaxZ
	BEGIN 
		insert into #ChildLocParentLocPairingCandidates (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
			SELECT L.ID as ID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
			 from @MissingLocIDs Missing
			inner join Location L on L.ID = Missing.ID
			inner join Structure Child on Child.ID = L.ParentID 
			inner join Location PL on PL.ParentID = Child.ParentID
			where Child.ParentID is not NULL 
				AND ABS(L.Z - PL.Z) <= @AllowedZRange  

		delete from @MissingLocIDs WHERE ID in (Select ID from #ChildLocParentLocPairingCandidates)

		--select COUNT(ID) as NumRemaining from @MissingLocIDs 

		set @AllowedZRange = @AllowedZRange + 1
	end
END
go

--select top 100 * from #ChildLocParentLocPairingCandidates
 
print 'Calculating child->parent candidate attachment point distances'
go

if OBJECT_ID('tempdb..#ChildLocParentDistance') is null
	/*Insert approximate centerpoints into a temporary table*/
	select C.ChildLocationID  as ChildLocationID,   C.ParentLocationID  as ParentLocationID,
	       C.ChildStructureID as ChildStructureID, C.ParentStructureID as ParentStructureID,
		   [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) as Distance
		into #ChildLocParentDistance
		from #ChildLocParentLocPairingCandidates C --Candidate C
	inner join Location L on L.ID = C.ChildLocationID
	inner join Structure Child on Child.ID = C.ChildStructureID 
	inner join Location PL on PL.ID = C.ParentLocationID
	order by C.ChildStructureID, C.ChildLocationID

select * from #ChildLocParentDistance

print 'Calculating minumum child->parent candidate attachment point distances'
go

--Record which Location ID pairs are the closest point of approach for locations
if OBJECT_ID('tempdb..#MinChildLocParentDistance') is null
	/*Insert approximate centerpoints into a temporary table*/
	select CPD.ChildStructureID as ID, MIN( CPD.Distance) as Distance
		into #MinChildLocParentDistance
		from #ChildLocParentDistance CPD --Candidate C
		group by CPD.ChildStructureID
		order by CPD.ChildStructureID
		
select * from #MinChildLocParentDistance

--Determine which child structures have multiple results, use the nearest location to the center of the child
 
print 'Identifying child->parent attachment points by minimum distance'
go

if OBJECT_ID('tempdb..#ChildAttachLocations') is NULL
	select CPD.ChildLocationID, CPD.ChildStructureID, CPD.ParentLocationID, CPD.ParentStructureID, CMin.Distance as Distance 
		into #ChildAttachLocations
		from #ChildLocParentDistance CPD
		inner join #MinChildLocParentDistance CMin ON CMin.ID = CPD.ChildStructureID
		where CPD.Distance = CMin.Distance 
go

select * from #ChildAttachLocations

print 'Count number of edge candidates remaining for each child structure'
go

if OBJECT_ID('tempdb..#ChildStructureEdgeCount') is NULL
select CAL.ChildStructureID, count(CAL.ChildStructureID) as NumEdgeCandidates 
	into #ChildStructureEdgeCount
	from #ChildAttachLocations CAL
	group by CAL.ChildStructureID
	

select * from #ChildStructureEdgeCount order by ChildStructureID
	


---------------------------------------------

print 'Finding centers for children with multiple edge candidates remaining'
go

declare @MultipleEdgeStructures integer_list
insert into @MultipleEdgeStructures
	select CAL.ChildStructureID from #ChildStructureEdgeCount CAL
	where CAL.NumEdgeCandidates > 1

	
select * from @MultipleEdgeStructures order by ID

/*Declaring a local variable as table, so remember not to use 'go' until we are done with it*/  
DECLARE @ChildStructureCenters TABLE(
		StructureID bigint NOT NULL,
		X Float NOT NULL,
		Y Float NOT NULL,
		Z Float NOT NULL
	);  
	 
insert into @ChildStructureCenters EXECUTE SelectStructureCenter3D @MultipleEdgeStructures

select * from @ChildStructureCenters
	
print 'Refining multiple edge candidates using weighted child structure center distance'

if OBJECT_ID('tempdb..#ChildAttachLocationsRefinedDistances') is null
  select CAL.ChildLocationID  as ChildLocationID,   CAL.ParentLocationID  as ParentLocationID,
	     CAL.ChildStructureID as ChildStructureID,  CAL.ParentStructureID as ParentStructureID, 
	     [dbo].[ufnShapeDistance3D]( Geometry::Point(CSC.X, CSC.Y,0), CSC.Z, ParentLoc.VolumeShape, ParentLoc.Z) as Distance
	into #ChildAttachLocationsRefinedDistances
	from #ChildAttachLocations CAL
	inner join [Location] ParentLoc ON ParentLoc.ID = CAL.ParentLocationID
	inner join @ChildStructureCenters CSC ON CSC.StructureID = CAL.ChildStructureID

-----------------------------------------------
	 
print 'Finding minimum distance from weighted child structure center distances'
go

if OBJECT_ID('tempdb..#MinChildAttachLocationsRefinedDistances') is null
  select [CARD].ChildStructureID as ChildStructureID,  
	     MIN([CARD].Distance) as MinDistance
	into #MinChildAttachLocationsRefinedDistances
	from #ChildAttachLocationsRefinedDistances [CARD]
	group by [CARD].ChildStructureID
	
print 'Reducing multiple edge child<->parent attachment points'
go

if OBJECT_ID('tempdb..#RefinedChildAttachLocations') is NULL
	select [CARD].ChildLocationID, [CARD].ChildStructureID, [CARD].ParentLocationID, [CARD].ParentStructureID, [CARD].Distance as Distance 
		into #RefinedChildAttachLocations
		from #ChildAttachLocationsRefinedDistances [CARD]
		inner join #MinChildAttachLocationsRefinedDistances CMin ON CMin.ChildStructureID = [CARD].ChildStructureID
		where [CARD].Distance = CMin.MinDistance 
		order by [CARD].ChildStructureID
go

select ChildStructureID, COUNT(ChildStructureID) as [Count] from #RefinedChildAttachLocations
	group by ChildStructureID
	order by ChildStructureID


print 'Appending child->parent attachment points without duplicate edges into result set'
go

insert into #RefinedChildAttachLocations (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID, Distance)
	 (select  CAL.ChildLocationID  as ChildLocationID,    CAL.ChildStructureID as ChildStructureID,
			  CAL.ParentLocationID  as ParentLocationID,  CAL.ParentStructureID as ParentStructureID, 
			CAL.Distance as Distance
		from #ChildAttachLocations CAL
		inner join #ChildStructureEdgeCount CEC ON CEC.ChildStructureID = CAL.ChildStructureID
		where CEC.NumEdgeCandidates = 1
		)

select COUNT(*) from #RefinedChildAttachLocations

select COUNT(*) from #RefinedChildAttachLocations
	inner join Structure S on S.ID = ChildStructureID

print 'Inserting child->parent attachment points into graph'
go
		
INSERT INTO graph.StructureAttachLocation($from_id, $to_id, FromStructureID, ToStructureID, Distance)
	select ChildLoc.NodeID, ParentLoc.NodeID, CAL.ChildStructureID, CAL.ParentStructureID, CAL.Distance
	from #RefinedChildAttachLocations CAL
		INNER JOIN (Select $node_id as NodeID, ID from graph.Location) ParentLoc on CAL.ParentLocationID = ParentLoc.ID
		INNER JOIN (Select $node_id as NodeID, ID from graph.Location) ChildLoc  on CAL.ChildLocationID  = ChildLoc.ID
		
print 'Inserting parent->child attachment points into graph (adding the reciprocal edges)'
go

INSERT INTO graph.StructureAttachLocation($from_id, $to_id, FromStructureID, ToStructureID, Distance)
	select ParentLoc.NodeID, ChildLoc.NodeID, CAL.ParentStructureID, CAL.ChildStructureID, CAL.Distance
	from #RefinedChildAttachLocations CAL
		INNER JOIN (Select $node_id as NodeID, ID from graph.Location) ParentLoc on CAL.ParentLocationID = ParentLoc.ID
		INNER JOIN (Select $node_id as NodeID, ID from graph.Location) ChildLoc  on CAL.ChildLocationID  = ChildLoc.ID

DROP TABLE IF EXISTS tempdb.#ChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#MinChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#ChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#MinChildLocParentDistance
DROP TABLE IF EXISTS tempdb.#ChildAttachLocations

/*
IF OBJECT_ID('tempdb..#ParentAttachment') is null
	select Child.X, Child.Y, ROUND(Child.Z,0, int) from #ChildLocCenters  


/********************************************************/
/* Original query for mixed synapses below */
/********************************************************/

/*Create a map of from every child structure to the opposite cell ID*/
if OBJECT_ID('tempdb..#Connection') is null
	select S1.ID as ID,
		   S1.ParentID as ParentID,
		   S1.TypeID as TypeID,
		   S1_OppStruct.ParentID as OppositeCellID,
		   S1_OppStruct.ID as OppositeChildID
		   into #Connection
	from Structure S1
		   inner join StructureLink SL1 on SL1.SourceID = S1.ID or SL1.TargetID = S1.ID
		   inner join Structure S1_OppStruct on S1_OppStruct.ID = case when
						SL1.SourceID = S1.ID then SL1.TargetID else SL1.SourceID
					end

/*Create a list of every child structure linked to the same opposite cell as another child structure*/
if OBJECT_ID('tempdb..#DistancePairs') is null
	Select S1.ID as S1_ID, S1.OppositeChildID as S1_OppID,
		   S2.ID as S2_ID, S2.OppositeChildID as S2_OppID,
		   S1.TypeID as S1_TypeID, S2.TypeID as S2_TypeID,
		   S1.ParentID as Cell, S1.OppositeCellID as OppositeCellID,
		   SQRT(POWER(L1.X - L2.X,2) + POWER(L1.Y - L2.Y,2) + POWER(L1.Z - L2.Z,2)) as Distance_um
		   into #DistancePairs
		   from #Connection S1
		inner join #Connection S2 on S2.OppositeCellID = S1.OppositeCellID
		inner join #LocCenters L1 on L1.ID = S1.ID
		inner join #LocCenters L2 on L2.ID = S2.ID 
		where S1.ID != S2.ID AND
			  S1.ParentID = S2.ParentID 
		order by Cell, OppositeCellID, Distance_um

/*
select * from #DistancePairs 
order by Cell, OppositeCell, S1_ID, Distance_um
*/

/*Identify the minimum distance between each pair of each type*/
if OBJECT_ID('tempdb..#MinDistancePair') is null
	select P.S1_ID as S1_ID, P.S2_TypeID as PairTypeID,
		   Min(P.Distance_um) as Min_distance
		into #MinDistancePair
		from #DistancePairs P
		where P.S1_TypeID != 28 
		AND P.S2_TypeID = 28 
		group by P.S1_ID, P.S2_TypeID
		order by P.S1_ID

/*select * from #MinDistancePair P order by P.S1_ID*/

if OBJECT_ID('tempdb..#MixedSynapses') is null
/*Select only the pairs with the minimum distance from the list of all possible mixed synapses*/
	select D.Cell as CellID, D.OppositeCellID as OppositeCellID,
		D.S1_ID as S1_ID, D.S1_TypeID as S1_TypeID, T1.Name as S1_Type, D.S1_OppID as S1_OppositeID, S1.Confidence as S1_Confidence,
		D.S2_ID as S2_ID, D.S2_TypeID as S2_TypeID, T2.Name as S2_Type, D.S2_OppID as S2_OppositeID, S2.Confidence as S2_Confidence,
		D.Distance_um as Distance_um
	into #MixedSynapses
	from #DistancePairs D
	inner join #MinDistancePair M on M.S1_ID = D.S1_ID and M.PairTypeID = D.S2_TypeID
	inner join Structure S1 on S1.ID = D.S1_ID
	inner join Structure S2 on S2.ID = D.S2_ID
	inner join StructureType T1 on T1.ID = S1_TypeID
	inner join StructureType T2 on T2.ID = S2_TypeID
	where M.Min_distance = D.Distance_um
	AND D.S2_TypeID = 28
	order by D.Cell, D.OppositeCellID, D.S1_ID

/*Identify all cases where the mixed synapse using min distance is also a mixed synapse using min-distance in the opposite cell.  This removes duplications in the results. */
if OBJECT_ID('tempdb..#JoinableMixedSynapses') is null
	select M1.CellID as Cell_A_ID, 
	   M2.CellID as Cell_B_ID,
	   M1.S1_ID as SideA_S1_ID, M1.S1_TypeID as SideA_S1_TypeID,  M1.S1_Type as SideA_S1_Type,  M1.S1_Confidence as SideA_S1_Confidence, 
	   M1.S2_ID as SideA_S2_ID, M1.S2_TypeID as SideA_S2_TypeID,  M1.S2_Confidence as SideA_S2_Confidence, 
	   M1.Distance_um as SideA_S1_to_S2_Distance,
	   M2.S1_ID as SideB_S1_ID, M2.S1_TypeID as SideB_S1_TypeID, M2.S1_Type as SideB_S1_Type,  M2.S1_Confidence as SideB_S1_Confidence, 
	   M2.S2_ID as SideB_S2_ID, M2.S2_TypeID as SideB_S2_TypeID, M2.S2_Type as SideB_S2_Type,  M2.S2_Confidence as SideB_S2_Confidence, 
	   M2.Distance_um as SideB_S1_to_S2_Distance
	into #JoinableMixedSynapses
	   from #MixedSynapses M1
	inner join #MixedSynapses M2 on M1.OppositeCellID = M2.CellID and
									M1.S1_OppositeID = M2.S1_ID and
									M1.S2_OppositeID = M2.S2_ID
	order by M1.CellID, M2.CellID, M1.S1_ID, M2.S1_ID

/*List all of the mixed synapses with thier mixed synapse partners where both synapses pass the minimum distance criteria*/
select * from #JoinableMixedSynapses J1
	where J1.Cell_A_ID < J1.Cell_B_ID
	order by J1.Cell_A_ID, J1.Cell_B_ID, J1.SideA_S1_ID, J1.SideA_S2_ID

/*Identify cases where the opposite mixed synapse is not in the above result set because it is not passing the minimum distance criteria*/
select M1.*, J1.SideB_S1_ID from #MixedSynapses M1
	LEFT outer join #JoinableMixedSynapses J1 on J1.Cell_A_ID = M1.CellID AND
												 J1.SideA_S1_ID = M1.S1_ID AND
												 J1.SideA_S2_ID = M1.S2_ID
	where J1.SideA_S1_ID is NULL
    order by M1.CellID, M1.S1_ID, M1.S2_ID

	*/

/*
if OBJECT_ID('tempdb..#LocCenters') is not null
	DROP Table #LocCenters

if OBJECT_ID('tempdb..#DistanceTable') is not null
	DROP Table #DistanceTable

if OBJECT_ID('tempdb..#MinDistance') is not null
	DROP Table #MinDistance
*/

/* Todo, group varicosities, measure distances between gap junctions and other synapses within the same varicosity 
	Write a SQL function to return the closest location ID on the Parent Cell for a synapse/gap junction*/
