/*
select L.ParentID as id, Geometry::UnionAggregate(L.VolumeShape) as shape from Location L
where L.ParentID = 593
and L.Radius >= 100
and L.Z > 80
group by L.ParentID

union
select S.ID as id, Geometry::UnionAggregate(L.VolumeShape)  as shape from Location L 
 inner join Structure S on S.ID = L.ParentID
where S.ParentID = 593
group by S.ID
*/

if OBJECT_ID('tempdb..#LocCenters') is not null
	DROP Table #LocCenters

/*Insert approximate centerpoints into a temporary table*/
select L.ParentID as ID, AVG(L.VolumeX) * dbo.XYScale() as X , AVG(L.VolumeY) * dbo.XYScale() as Y, AVG(L.Z) * dbo.ZScale() as Z into #LocCenters
from Location L
group by L.ParentID

select S.ID as S_id, S2.ID as S2_id, 
	   S.TypeID as S_typeid, S2.TypeID as S2_typeid,
	   L.X as X, L.Y as Y, L.Z as Z,
	   L2.X as X2, L2.Y as Y2, L2.Z as Z2,
	   SQRT(POWER(L.X - L2.X,2) + POWER(L.Y - L2.Y,2) + POWER(L.Z - L2.Z,2)) as Distance
	   from Structure S 
inner join Structure S2 on S2.ParentID = S.ParentID
inner join #LocCenters L on L.ID = S.ID
inner join #LocCenters L2 on L2.ID = S2.ID
where S.ParentID = 593 and S2.ParentID = 593 and S.TypeID = 28
order by S.ID, S2.ID


if OBJECT_ID('tempdb..#LocCenters') is not null
	DROP Table #LocCenters


/*
select S.TypeID, Geometry::UnionAggregate(L.VolumeShape) as shape from Location L
	inner join Structure S on S.ID = L.ParentID
where L.ParentID = 593
and L.Radius >= 100
and L.Z > 80
group by L.ParentID, S.TypeID

union all
select S.TypeID, Geometry::UnionAggregate(L.VolumeShape) as shape from Location L 
 inner join Structure S on S.ID = L.ParentID
where S.ParentID = 593
group by S.TypeID

*/

/* Todo, group varicosities, measure distances between gap junctions and other synapses within the same varicosity 
	Write a SQL function to return the closest location ID on the Parent Cell for a synapse/gap junction*/
