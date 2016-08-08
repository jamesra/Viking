declare @IDs  integer_list
declare @NetworkCenter bigint

set @NetworkCenter = 593
  
insert into @IDs 
	select distinct SourceID as ID from StructureLink 
		inner join Structure SStruct ON SStruct.ID = SourceID 
		where SStruct.ParentID = @NetworkCenter
	union 
	select distinct TargetID as ID from StructureLink 
		inner join Structure SStruct ON SStruct.ID = SourceID 
		where SStruct.ParentID = @NetworkCenter
	union 
	select distinct SourceID as ID from StructureLink 
		inner join Structure TStruct ON TStruct.ID = TargetID 
		where TStruct.ParentID = @NetworkCenter
	union 
	select distinct TargetID as ID from StructureLink 
		inner join Structure TStruct ON TStruct.ID = TargetID 
		where TStruct.ParentID = @NetworkCenter
	

select * from @IDs order by ID
   
select S.ID as ConnectionStructure, S.ParentID as CellID, SP.Label as CellLabel,
	   S.TypeID as SourceTypeID, 
	   dbo.ufnStructureArea(LocIDs.ID) as Area_nm,
	   dbo.ufnStructureArea(LocIDs.ID) / 1000000.0 as Area_um
from @IDs LocIDs
	join Structure S ON S.ID = LocIDs.ID
	join Structure SP ON SP.ID = S.ParentID
	order by LocIDs.ID