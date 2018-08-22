declare  @StartZ int
declare  @EndZ int

set @StartZ = 407
set @EndZ = 408

delete from LocationLink where A in 
	(select ID from Location where Z >= @StartZ and Z <= @endZ)
	or B in
	(select ID from Location where Z >= @StartZ and Z <= @endZ)

DELETE Location WHERE Z >= @StartZ and Z <= @EndZ