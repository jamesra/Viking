Use Rabbit
select Count(ID) as NumAnnotationsRC1 from Location
select Count(ID) as NumCellsRC1 from Structure where ParentID IS NULL
select Count(ID) as NumChildStructuresRC1 from Structure where ParentID IS NOT NULL

Use RC2
select Count(ID) as NumAnnotationsRC2 from Location
select Count(ID) as NumCellsRC2 from Structure where ParentID IS NULL
select Count(ID) as NumChildStructuresRC2 from Structure where ParentID IS NOT NULL

Use RPC1
select Count(ID) as NumAnnotationsRPC1 from Location
select Count(ID) as NumCellsRPC1 from Structure where ParentID IS NULL
select Count(ID) as NumChildStructuresRPC1 from Structure where ParentID IS NOT NULL