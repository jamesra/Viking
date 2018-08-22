		  
select num, SourceID, TargetID from 
	(select count(SourceID) as num, SourceID, TargetID from StructureLink Group by SourceID, TargetID )  as L
	where L.num > 1