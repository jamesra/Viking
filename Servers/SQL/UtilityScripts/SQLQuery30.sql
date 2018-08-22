declare @Z float
declare @QueryDate datetime
declare @StructsOnSection TABLE 
(
	ID bigint
)

if @QueryDate <> NULL
	INSERT INTO @StructsOnSection (ID)
	Select distinct(ParentID) from Location where Z = @Z
else
	INSERT INTO @StructsOnSection (ID)
	Select distinct(ParentID) from Location where Z = @Z
	
Select * from Structure
where ID in (Select ID from @StructsOnSection)

Select * from StructureLink L
where (L.TargetID in (Select ID from @StructsOnSection))
   OR (L.SourceID in (Select ID from @StructsOnSection))