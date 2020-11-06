if OBJECT_ID('tempdb..#Connections') is not null
	DROP Table #Connections

if OBJECT_ID('tempdb..#ZLevels') is not null
	DROP Table #ZLevels

if OBJECT_ID('tempdb..#ConnectionRanges') is not null
	DROP Table #ConnectionRanges

if OBJECT_ID('tempdb..#SomaCutoffZ') is not null
	DROP Table #SomaCutoffZ

declare @TagName nvarchar(128)
set @TagName = 'Study Specific Marker'

	
select Cell.ID as Cell_ID, Synapse.ID as Synapse_ID, PSD.ID as PSD_ID, PSD_Cell.ID as RodBC_ID 
	into #Connections
	from Structure Synapse 
    inner join Structure Cell on Cell.ID = Synapse.ParentID
	inner join StructureLink Link on Link.SourceID = Synapse.ID
	inner join Structure PSD on Link.TargetID = PSD.ID
	inner join Structure PSD_Cell on PSD_Cell.ID = PSD.ParentID
	where 
		/*PSD_Cell.Label like '%RodBC%' and */
		PSD.TypeID = 35 and Synapse.TypeID = 73
		and PSD_Cell.ID = 822
	order by PSD_Cell.ID, Cell.ID  


select Synapse.ID as SyanpseID, AVG(L.Z) as AvgZ, AVG(L.Z) - 5 as MinZ, AVG(L.Z) + 5 as MaxZ into #ZLevels from Location L 
	inner join Structure Synapse on Synapse.ID = L.ParentID
	inner join #Connections Connection on Connection.Synapse_ID = Synapse.ID
	inner join Location SynapseAnnotations on SynapseAnnotations.ParentID = Synapse.ID 
	Group BY Synapse.ID

select C.*, Z.MinZ, Z.AvgZ, Z.MaxZ 
	into #ConnectionRanges
	from #ZLevels Z
	inner join #Connections C on C.Synapse_ID = Z.SyanpseID

select L.ParentID as StructureID, MAX(L.Z) as Z into #SomaCutoffZ from Location L  where
 dbo.LocationHasTag(L.ID, @TagName) = 1 
 group by L.ParentID


/*select * from #ConnectionRanges*/

select A.*, S.Label from (select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
	from Location L
	inner join #ConnectionRanges CR on CR.Synapse_ID = L.ParentID
	where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
	group by L.ParentID
union all
 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
	from Location L
	inner join (select distinct Cell_ID as Cell_ID, MIN(MinZ) as MinZ, MAX(MaxZ) as MaxZ from #ConnectionRanges group by Cell_ID) CR on CR.Cell_ID = L.ParentID /*use of distinct eliminates multiple identical join rows being merged into the geometry */
	where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
	group by L.ParentID
union all
 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
	from Location L
	inner join #ConnectionRanges CR on CR.PSD_ID = L.ParentID
	where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
	group by L.ParentID
union all
 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
	from Location L
	inner join (select distinct RodBC_ID as RodBC_ID,  MIN(MinZ) as MinZ, MAX(MaxZ) as MaxZ  from #ConnectionRanges group by RodBC_ID) CR on CR.RodBC_ID = L.ParentID /*use of distinct eliminates multiple identical join rows being merged into the geometry */
	join #SomaCutoffZ SC on SC.StructureID = CR.RodBC_ID
	where L.Z <= SC.Z and L.TypeCode != 1 /*L.Z <= 400 *//*L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1*/
	group by L.ParentID) as A
inner join Structure S on S.ID = A.ID 

select Shape, Shape.ToString(), Label from (
	select Geometry::UnionAggregate(A.Shape) as Shape, S.Label as Label from (select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
		from Location L
		inner join #ConnectionRanges CR on CR.Synapse_ID = L.ParentID
		where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
		group by L.ParentID
	union all
	 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
		from Location L
		inner join (select distinct Cell_ID as Cell_ID, MIN(MinZ) as MinZ, MAX(MaxZ) as MaxZ from #ConnectionRanges group by Cell_ID) CR on CR.Cell_ID = L.ParentID /*use of distinct eliminates multiple identical join rows being merged into the geometry */
		where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
		group by L.ParentID
	union all
	 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
		from Location L
		inner join #ConnectionRanges CR on CR.PSD_ID = L.ParentID
		where L.Z >= CR.MinZ and L.Z <= CR.MaxZ and L.TypeCode != 1
		group by L.ParentID
	union all
	 select L.ParentID as ID, Geometry::UnionAggregate(L.VolumeShape) as Shape
		from Location L
		inner join (select distinct RodBC_ID as RodBC_ID,  MIN(MinZ) as MinZ, MAX(MaxZ) as MaxZ  from #ConnectionRanges group by RodBC_ID) CR on CR.RodBC_ID = L.ParentID /*use of distinct eliminates multiple identical join rows being merged into the geometry */
		join #SomaCutoffZ SC on SC.StructureID = CR.RodBC_ID
		where L.Z <= SC.Z and L.TypeCode != 1
		group by L.ParentID) as A
	inner join Structure S on S.ID = A.ID 
	group by S.Label
) as A