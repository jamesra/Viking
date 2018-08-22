IF OBJECT_ID ('dbo.Temp', 'U') IS NOT NULL
    DROP TABLE dbo.Temp;
GO

use Rabbit

Create Table Temp (SourceID BigInt,
				   TargetID BigInt,
				   Bidirectional bit,
				   Tags XML,
				   Username ntext, 
				   Created datetime,
				   LastModified datetime)

declare @TypeID bigint
SET @TypeID = 35

Insert Into Temp 
	select SL.SourceID, SL.TargetID, SL.Bidirectional, SL.Tags, SL.Username, SL.Created, SL.LastModified from StructureLink SL
	inner Join Structure SSource
	on SSource.ID = SL.SourceID
	where TypeID = @TypeID
	
Insert INTO StructureLink 
	select * from Temp
	
delete from StructureLink 
from StructureLink as SL
	inner Join Structure as SSource
	on SSource.ID = SourceID
	where TypeID = @TypeID
	
DROP Table Temp

/*Update structure links so only gap junctions and desmosomes are bidirectional*/
update StructureLink
	set Bidirectional = 0
	from StructureLink SL
	inner Join Structure STarget
	on STarget.ID = SL.TargetID
	where TypeID = @TypeID AND Bidirectional = 1
	