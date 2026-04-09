declare @ZA int 
declare @ZB int 

set @ZA = 1034
set @ZB = 1032

if OBJECT_ID('tempdb..#LocationsInZRange') is not null
	DROP Table #LocationsInZRange

CREATE TABLE #LocationsInZRange(ID bigint)

insert into #LocationsInZRange (ID)
	select ID From Location L where Z = @ZA
insert into #LocationsInZRange (ID)
	select ID From Location L where Z = @ZB
	 
Update L set Z = -@ZA from Location L where L.Z = @ZA
Update L set Z = @ZA from Location L where L.Z = @ZB
Update L set Z = @ZB from Location L where L.Z = -@ZA

DELETE LocationLink WHERE
	A IN (select ID from #LocationsInZRange )
	AND 
	B NOT IN (select ID from #LocationsInZRange )

DELETE LocationLink WHERE
	A NOT IN (select ID from #LocationsInZRange )
	AND 
	B IN (select ID from #LocationsInZRange )

DELETE LocationLink WHERE
	A IN (select ID from #LocationsInZRange )
	AND 
	B IN (select ID from #LocationsInZRange )
	
--DROP Table #LocationsInZRange