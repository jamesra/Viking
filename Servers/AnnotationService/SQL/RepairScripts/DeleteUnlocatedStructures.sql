delete from StructureLink where TargetID = SourceID
go

delete from StructureLink  where TargetID IN
 
	(select ID from Structure where ID not in 
	(select ParentID from Location group by ParentID)
	)
go
	
delete from StructureLink  where SourceID IN
 
	(select ID from Structure where ID not in 
	(select ParentID from Location group by ParentID)
	)
go

delete from Structure 
	where ID not in 
		(select ParentID from Location group by ParentID)

select ID from Structure
where ParentID in (
select ID from Structure 
	where ID not in 
		(select ParentID from Location group by ParentID))

/*select * from Structure as S where S.ParentID in
(select ID from Structure where ID not in 
	(select ParentID from Location group by ParentID)
	)
*/
