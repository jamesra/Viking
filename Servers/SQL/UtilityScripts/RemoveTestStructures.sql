select * from Location where ParentID in 
	(select ID from Structure where TypeID in 
		(select ID from StructureType where Name = 'Test'))

delete from StructureLink where SourceID in 
	(select ID from Structure where TypeID in 
		(select ID from StructureType where Name = 'Test'))

delete from StructureLink where TargetID in 
	(select ID from Structure where TypeID in 
		(select ID from StructureType where Name = 'Test'))

delete from Structure where TypeID in 
	(select ID from StructureType where Name = 'Test')