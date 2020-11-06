/*********************************************
/*Run this block of code if you change any of the first three queries or annotations for the structure.  It will clear the cache.*/

if OBJECT_ID('tempdb..#LocCenters') is not null
	DROP Table #LocCenters

if OBJECT_ID('tempdb..#DistanceTable') is not null
	DROP Table #DistanceTable

if OBJECT_ID('tempdb..#MinDistance') is not null
	DROP Table #MinDistance

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

if OBJECT_ID('tempdb..#DistanceTable') is null
	select 
	   S.ParentID as Cell_id,
	   S.ID as S_id, S2.ID as S2_id,
	   S.Confidence as S1_Confidence, S2.Confidence as S2_Confidence,
	   S.TypeID as S_typeid, S2.TypeID as S2_typeid,
	   L.X as X, L.Y as Y, L.Z as Z,
	   L2.X as X2, L2.Y as Y2, L2.Z as Z2,
	   SQRT(POWER(L.X - L2.X,2) + POWER(L.Y - L2.Y,2) + POWER(L.Z - L2.Z,2)) as Distance_um
	   into #DistanceTable
	   from Structure S 
		inner join Structure S2 on S2.ParentID = S.ParentID
		inner join #LocCenters L on L.ID = S.ID
		inner join #LocCenters L2 on L2.ID = S2.ID
	where
	 S.ParentID = S2.ParentID AND
	 S.TypeID = 28 and S2.TypeID != 28
	 /*AND S.ParentID = 593 */  /*Uncomment this line to limit to a specific parent cell, or add criteria here for classes of cells*/
	order by Cell_id, S.ID, S2.ID

if OBJECT_ID('tempdb..#MinDistance') is null
	select L.S2_id as S2_id, Min(L.Distance_um) as Min_distance 
		into #MinDistance
		from #DistanceTable L
		group by L.S2_id 

select D.Cell_id,
	   D.S_id, D.S2_id, 
	   D.S1_Confidence, D.S2_Confidence,
	   D.S_typeid, D.S2_typeid,			
	   D.Distance_um,				     /*Distance from Structure 1 and 2 in microns*/
	   S1_OppStruct.ID as S1_Opposite,   /*Child struct opposite structure 1*/
	   S2_OppStruct.ID as S2_Opposite,   /*Child struct opposite structure 2*/
	   S1_OppStruct.ParentID as Opposite_Parent /*Shared target cell of structure 1 & 2 opposites*/
	     from #DistanceTable D
 inner join #MinDistance M on M.S2_id = D.S2_id
 inner join StructureLink SL1 on SL1.SourceID = D.S_id or SL1.TargetID = D.S_id
 inner join StructureLink SL2 on SL2.SourceID = D.S2_id or SL2.TargetID = D.S2_id
 inner join Structure S1_OppStruct on S1_OppStruct.ID = case when
			SL1.SourceID = D.S_id then SL1.TargetID else SL1.SourceID 
	   end
 inner join Structure S2_OppStruct on S2_OppStruct.ID = case when
			SL2.SourceID = D.S2_id then SL2.TargetID else SL2.SourceID 
	   end
 where M.Min_distance = D.Distance_um 
	and S1_OppStruct.ParentID = S2_OppStruct.ParentID
 order by D.Cell_id, D.S_id, D.S2_id

 /* ***Select the opposite parent cell for a given link of a cell ***

select case 
		when SourceStruct.ParentID != 593 then SourceStruct.ParentID 
		else TargetStruct.ParentID
	end as OppositeID from StructureLink SL 
	inner join Structure SourceStruct on SourceStruct.ID = SL.SourceID
	inner join Structure TargetStruct on TargetStruct.ID = SL.TargetID
	where SourceStruct.ParentID = 593 OR TargetStruct.ParentID = 593

**********************************************************************/


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
