declare @TagName nvarchar(128)
set @TagName = 'Study Specific Marker'

if OBJECT_ID('tempdb..#SomaCutoffZ') is not null
	DROP Table #SomaCutoffZ
	 
select L.ParentID as StructureID, MAX(L.Z) as Z into #SomaCutoffZ from Location L  where
 dbo.LocationHasTag(L.ID, @TagName) = 1 
 group by L.ParentID
  
 SELECT Geometry::UnionAggregate(L.VolumeShape) as Shape, S.Label as Label, S.ID as ID from Location L
	inner join Structure S on S.ID = L.ParentID
	join #SomaCutoffZ SC on SC.StructureID = L.ParentID
	where L.Z <= SC.Z AND L.TypeCode != 1 and
	S.Label = 'RodBC'
	Group by S.ID, S.Label