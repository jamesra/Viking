use Rabbit

delete from StructureLink where TargetID = SourceID

update Structure
	set ParentID = (select ParentID from Structure Parent where Parent.ID = ParentID)
	where (ParentID in
	 (select ID from Structure Parents where Parents.TypeID <> 1))
	 
