declare @IDs  mem_integer_list

declare @NetworkCenter bigint

set @NetworkCenter = 514

 

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


(select SourceParent.ID as SourceCellID,
       SourceParent.Label as SourceCellLabel,
	   SL.SourceID as SourceID,
	   SourceType.ID as SourceTypeID,
	   SourceType.Name as SourceType,
	   Source.Confidence as SourceConfidence,
	   dbo.ufnStructureArea(SL.SourceID) as SourceArea_nm,
	   dbo.ufnStructureArea(SL.SourceID) / 1000000.0 as SourceArea_um,
       TargetParent.ID as TargetCellID,
	   TargetParent.Label as TargetCellLabel,
	   SL.TargetID as TargetID,
	   TargetType.ID as TargetTypeID,
	   TargetType.Name as TargetType,
	   Target.Confidence as TargetConfidence,
	   dbo.ufnStructureArea(SL.TargetID) as TargetArea_nm,
	   dbo.ufnStructureArea(SL.TargetID) / 1000000.0 as TargetArea_um

           from StructureLink SL

       inner join Structure Source on Source.ID = SL.SourceID

       inner join Structure SourceParent on SourceParent.ID = Source.ParentID

       inner join StructureType SourceType on SourceType.ID = Source.TypeID

       inner join Structure Target on Target.ID = SL.TargetID

       inner join Structure TargetParent on TargetParent.ID = Target.ParentID

       inner join StructureType TargetType on TargetType.ID = Target.TypeID

       INNER JOIN @IDs as I ON I.ID = SL.SourceID 

UNION
	select SourceParent.ID as SourceCellID,
       SourceParent.Label as SourceCellLabel,
	   SL.SourceID as SourceID,
	   SourceType.ID as SourceTypeID,
	   SourceType.Name as SourceType,
	   Source.Confidence as SourceConfidence,
	   dbo.ufnStructureArea(SL.SourceID) as SourceArea_nm,
	   dbo.ufnStructureArea(SL.SourceID) / 1000000.0 as SourceArea_um,
       TargetParent.ID as TargetCellID,
	   TargetParent.Label as TargetCellLabel,
	   SL.TargetID as TargetID,
	   TargetType.ID as TargetTypeID,
	   TargetType.Name as TargetType,
	   Target.Confidence as TargetConfidence,
	   dbo.ufnStructureArea(SL.TargetID) as TargetArea_nm,
	   dbo.ufnStructureArea(SL.TargetID) / 1000000.0 as TargetArea_um

           from StructureLink SL

       inner join Structure Source on Source.ID = SL.SourceID

       inner join Structure SourceParent on SourceParent.ID = Source.ParentID

       inner join StructureType SourceType on SourceType.ID = Source.TypeID

       inner join Structure Target on Target.ID = SL.TargetID

       inner join Structure TargetParent on TargetParent.ID = Target.ParentID

       inner join StructureType TargetType on TargetType.ID = Target.TypeID

       INNER JOIN @IDs as I ON I.ID = SL.TargetID)

       order by SourceCellID, TargetCellID, SourceID, TargetID
	    