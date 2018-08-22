declare @StartZ int 
declare @EndZ int 

set @StartZ = 255
set @EndZ = 257

if OBJECT_ID('tempdb..#LocationsInZRange') is not null
	DROP Table #LocationsInZRange

select ID INTO #LocationsInZRange from Location where Z >= @StartZ and Z <= @EndZ

DELETE LocationLink WHERE
	A IN (select ID from #LocationsInZRange )
	OR
	B IN (select ID from #LocationsInZRange )

DELETE Location WHERE ID in (select ID from #LocationsInZRange )

DROP Table #LocationsInZRange