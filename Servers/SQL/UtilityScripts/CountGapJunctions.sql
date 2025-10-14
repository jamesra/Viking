declare @GapJunctionStructures integer_list
insert into @GapJunctionStructures select ID from Structure where TypeID = 28

declare @GapJunctionLinks udtLinks
insert into @GapJunctionLinks select SourceID, TargetID from StructureLink 
	inner join Structure SourceStructure ON SourceStructure.ID = SourceID
	inner join Structure TargetStructure ON TargetStructure.ID = TargetID
	where SourceStructure.TypeID = 28 and TargetStructure.TypeID = 28

DELETE FROM @GapJunctionStructures  
	where ID IN (Select TargetID from @GapJunctionLinks)

DELETE FROM @GapJunctionStructures 
	where ID IN (Select SourceID from @GapJunctionLinks)

PRINT 'Unlinked gap junctions:'
select COUNT(ID) as NumUnlinkedGapJunctions FROM @GapJunctionStructures

PRINT 'Linked gap junctions:'
SELECT COUNT(SourceID) as NumLinkedGapJunctions from @GapJunctionLinks