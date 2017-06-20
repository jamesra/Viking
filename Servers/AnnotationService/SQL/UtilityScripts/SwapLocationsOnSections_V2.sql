/* A function to swap annotations on two adjacent sections while preserving links*/

declare @ZA int 
declare @ZB int 

set @ZA = 886
set @ZB = 887

if OBJECT_ID('tempdb..#AllLocationsInZRange') is not null
	DROP Table #AllLocationsInZRange
if OBJECT_ID('tempdb..#DeadLinks') is not null
	DROP Table #DeadLinks
if OBJECT_ID('tempdb..#LocationPairs') is not null
	DROP Table #LocationPairs
	
if OBJECT_ID('tempdb..#FinalMapping') is not null
	DROP Table #FinalMapping
	 
CREATE TABLE #AllLocationsInZRange(ID bigint)

insert into #AllLocationsInZRange (ID)
	select ID From Location L where Z = @ZA OR Z = @ZB
/*insert into #AllLocationsInZRange (ID)
	select ID From Location L where Z = @ZB
	*/
/*Swap the Z level of the sections */	 


CREATE TABLE #DeadLinks(IDofSwappedZ bigint, IDofConstantZ bigint) 
CREATE TABLE #LocationPairs(A bigint, B bigint)
CREATE TABLE #FinalMapping(NewA bigint, NewB bigint, IDofSwappedZ bigint, IDofConstantZ bigint)

insert into #LocationPairs (A,B)
	select A, B from LocationLink where
		A IN (select ID from #AllLocationsInZRange )
		AND 
		B IN (select ID from #AllLocationsInZRange )

insert into #LocationPairs (B,A)
	select A, B from LocationLink where
		A IN (select ID from #AllLocationsInZRange )
		AND 
		B IN (select ID from #AllLocationsInZRange )

--select * from #LocationPairs order by A

/* A table of links from swapped sections to adjacent unswapped sections */
insert into #DeadLinks (IDofSwappedZ, IDofConstantZ)
	Select A as IDofSwappedZ, B as IDofConstantZ from LocationLink Where
		A IN (select ID from #AllLocationsInZRange )
		AND 
		B NOT IN (select ID from #AllLocationsInZRange )

insert into #DeadLinks (IDofSwappedZ, IDofConstantZ)
	Select B as IDofSwappedZ, A as IDofConstantZ from LocationLink Where
		B IN (select ID from #AllLocationsInZRange )
		AND 
		A NOT IN (select ID from #AllLocationsInZRange )

insert into #FinalMapping (NewA, NewB, IDofSwappedZ, IDofConstantZ)
	select LP.B as NewA, DL.IDofConstantZ as NewB, DL.IDofSwappedZ, DL.IDofConstantZ from #DeadLinks DL 
	inner join #LocationPairs LP ON LP.A = DL.IDofSwappedZ

select LA.Z as AZ, LB.Z as BZ, * from #FinalMapping FM
inner join Location LA on LA.ID = FM.NewA 
inner join Location LB on LB.ID = FM.NewB

select Link.*, FM.NewA, FM.NewB from LocationLink Link 
	inner join #FinalMapping FM ON (Link.A = FM.IDofConstantZ AND Link.B = FM.IDofSwappedZ) OR 
								   (Link.B = FM.IDofConstantZ AND Link.A = FM.IDofSwappedZ)
	order by FM.NewA, FM.NewB

update Link set Link.A = FM.NewA, Link.B = FM.NewB from LocationLink Link 
	inner join #FinalMapping FM ON (Link.A = FM.IDofConstantZ AND Link.B = FM.IDofSwappedZ) OR 
								   (Link.B = FM.IDofConstantZ AND Link.A = FM.IDofSwappedZ)
		  
--Update Link Set A = (Select top IDof
  
DROP TABLE #AllLocationsInZRange
DROP TABLE #DeadLinks
DROP TABLE #LocationPairs
DROP Table #FinalMapping


Update L set Z = -@ZA from Location L where L.Z = @ZA
Update L set Z = @ZA from Location L where L.Z = @ZB
Update L set Z = @ZB from Location L where L.Z = -@ZA
