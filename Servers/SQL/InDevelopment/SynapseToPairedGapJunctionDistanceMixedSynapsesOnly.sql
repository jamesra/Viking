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

if OBJECT_ID('tempdb..#LocCenters') is null
	/*Insert approximate centerpoints into a temporary table*/
	select   L.ParentID as ID,
			 AVG(L.VolumeX) * dbo.XYScale() / 1000.0 as X,
			 AVG(L.VolumeY) * dbo.XYScale() / 1000.0 as Y,
			 AVG(L.Z) * dbo.ZScale() / 1000.0 as Z
	into #LocCenters
	from Location L
	inner join Structure S on S.ID = L.ParentID
	where S.TypeID != 1
	group by L.ParentID

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
