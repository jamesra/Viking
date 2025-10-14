declare @IDs  mem_integer_list
declare @NetworkCenter bigint

set @NetworkCenter = 514
  
insert into @IDs 
	select distinct S.ID as ID from Structure S
		inner join Structure Parent on S.ParentID = Parent.ID
		where Parent.ID = @NetworkCenter

select Parent.ID as CellID,
       Parent.Label as CellLabel,
	   Child.ID as ChildID,
	   ChildType.ID as ChildTypeID,
	   ChildType.Name as ChildType,
	   Child.Confidence as ChildConfidence,
	   dbo.ufnStructureArea(Child.ID) as SourceArea_nm,
	   dbo.ufnStructureArea(Child.ID) / 1000000.0 as SourceArea_um
       from Structure Child
       inner join Structure Parent on Child.ParentID = Parent.ID 
       inner join StructureType ChildType on ChildType.ID = Child.TypeID 
       INNER JOIN @IDs as I ON I.ID = Child.ID