IF OBJECT_ID ('dbo.Temp', 'U') IS NOT NULL
    DROP TABLE dbo.Temp;
GO

declare @TypeID bigint
SET @TypeID = 35 /* Update TypeID to match Structure Type we do not want to be a sourceID in links */

use RC1 /*Specifies which database to repair links on*/

Create Table Temp (SourceID BigInt,
				   TargetID BigInt,
				   Bidirectional bit,
				   Tags XML,
				   Username ntext, 
				   Created datetime,
				   LastModified datetime)


Insert Into Temp 
	select SL.TargetID as SourceID, /*Swap the Source and Target ID's*/
		   SL.SourceID as TargetID,
		   0 as Bidirectional, /*PSDs should not be directional*/
		   SL.Tags, 
		   SL.Username,
		   SL.Created,
		   SL.LastModified from StructureLink SL
	inner Join Structure SSource
	on SSource.ID = SL.SourceID
	where SSource.TypeID = @TypeID AND NOT SL.TargetID = Sl.SourceID /*If source and target ID match it is probably an accidental link */

/*select * from Temp */ /*Uncomment to list affected links*/
 
/*Remove the existing links from the database*/
delete from StructureLink 
from StructureLink as SL
	inner Join Structure as SSource
	on SSource.ID = SourceID
	where SSource.TypeID = @TypeID AND NOT SL.TargetID = SL.SourceID
	
/*Insert the swapped links*/
Insert INTO StructureLink 
	select * from Temp
	 
DROP Table Temp
/*
/*Update structure links so only gap junctions and desmosomes are bidirectional*/
update StructureLink
	set Bidirectional = 0
	from StructureLink SL
	inner Join Structure STarget
	on STarget.ID = SL.TargetID
	where TypeID = @TypeID AND Bidirectional = 1
	*/
	