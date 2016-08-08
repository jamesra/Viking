IF OBJECT_ID('tempdb..#StructureLinks') IS NOT NULL DROP TABLE #StructureLinks
IF OBJECT_ID('tempdb..#ExtraStructureLinks') IS NOT NULL DROP TABLE #ExtraStructureLinks
IF OBJECT_ID('tempdb..#ContactDuplicates') IS NOT NULL DROP TABLE #ContactDuplicates
IF OBJECT_ID('tempdb..#PairDistance') IS NOT NULL DROP TABLE #PairDistance
IF OBJECT_ID('tempdb..#NearestPartner') IS NOT NULL DROP TABLE #NearestPartner
IF OBJECT_ID('tempdb..#PairsToDelete') IS NOT NULL DROP TABLE #PairsToDelete

declare @IDs  integer_list
declare @NetworkCenter bigint

set @NetworkCenter = 593
  
insert into @IDs 
	 select distinct SourceID as ID from StructureLink 
		inner join Structure SStruct ON SStruct.ID = SourceID 
		where SStruct.ParentID = @NetworkCenter
	union 
	select distinct TargetID as ID from StructureLink 
		inner join Structure TStruct ON TStruct.ID = TargetID 
		where TStruct.ParentID = @NetworkCenter

select * from @IDs
   
select S.ParentID as SourceParentID, SP.Label as SourceLabel, SL.SParentID as SourceID,
	   T.ParentID as TargetParentID, TP.Label as TargetLabel, SL.TParentID as TargetID,
	   S.TypeID as SourceTypeID, T.TypeID as TargetTypeID,
	   sum( SourceLine.STLength()) * @AreaScalar as Source_Area_nm,
	   sum( SourceLine.STLength()) * @AreaScalar / 1000000.0 as Source_Area_um, --, geometry::CollectionAggregate(Shape) as shape 
	   sum( TargetLine.STLength()) * @AreaScalar as Target_Area_nm,
	   sum( TargetLine.STLength()) * @AreaScalar / 1000000.0 as Target_Area_um--, geometry::CollectionAggregate(Shape) as shape 
from @IDs LocIDs
	join Structure S ON S.ID = SL.SParentID
	join Structure T ON T.ID = SL.TParentID
	join Structure SP ON SP.ID = S.ParentID
	join Structure TP ON TP.ID = T.ParentID
	group by SL.SParentID, SL.TParentID, S.ParentID, T.ParentID, S.TypeID, T.TypeID, SP.Label, TP.Label
	order by SourceID