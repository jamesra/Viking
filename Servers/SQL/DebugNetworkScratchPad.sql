
declare @IDs  integer_list
declare @Hops int
set @Hops = 10

insert into @IDs (ID) values (476)

exec SelectNetworkStructureLinks @IDs, @Hops

select * from StructureLink

/*

--exec SelectNetworkStructureIDs @IDs, @Hops

--select * from NetworkStructureIDs(@IDs, @Hops)

exec SelectNetworkStructures @IDs, @Hops

exec SelectNetworkChildStructures @IDs, @Hops


select * from DBVersion order by DBVersionID
*/