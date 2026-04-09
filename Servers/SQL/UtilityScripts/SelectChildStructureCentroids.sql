DECLARE @TotalArea TABLE(
        StructureID bigint NOT NULL PRIMARY KEY,
        TotalArea float NOT NULL
    );

declare @GLPartner dbo.udtParentChildIDMap

Insert INTO @GLPartner
SELECT distinct  Child.ID, S.ID
from Structure S 
inner join Structure Child ON Child.ParentID = S.ID
WHERE
Child.TypeID = 28

DECLARE @Result TABLE(
	SourceParent bigint,
	SourceParentLabel VARCHAR(4096),
	SourceStructureType bigint,
	SourceID bigint,
    TargetID bigint,
	TargetParent bigint,
	TargetParentLabel VARCHAR(4096),
	Notes VARCHAR(4096),
	GJArea float, 
	SourceXY geometry,
	SourceZ float,
	TargetXY geometry,
	TargetZ float
)
 
insert into @Result select SParent.ID as SourceParent, SParent.Label as SourceParentLabel, S.TypeID as SourceStructureType, SourceID,
	   TargetID, TParent.ID as TargetParent, TParent.Label as TargetParentLabel, SParent.Notes as Notes,
	   [dbo].ufnStructureArea(TargetID) as GJArea, 
	   [dbo].[ufnWeightedStructureCentroidXY](SourceID) as SourceXY,
	   [dbo].[ufnWeightedStructureCentroidZ](SourceID) as SourceZ,
	   [dbo].[ufnWeightedStructureCentroidXY](TargetID).ToString() as TargetXY,
	   [dbo].[ufnWeightedStructureCentroidZ](TargetID) as TargetZ   
	
from StructureLink
	inner join Structure T on T.ID = TargetID
	inner join Structure S on S.ID = SourceID
	inner join Structure TParent on T.ParentID = TParent.ID
	inner join Structure SParent on S.ParentID = SParent.ID 
	inner join @GLPartner GJP on GJP.ID = TargetID
WHERE (TParent.ID = 514 OR SParent.ID = 514)
ORDER BY SourceParentLabel

select 
	 SourceParent, SourceParentLabel, SourceStructureType, SourceID,
	 TargetID, TargetParent, TargetParentLabel,  Notes, GJArea, 
	 SourceXY.STX as SourceX, SourceXY.STY as SourceY, SourceZ,
	 TargetXY.STX as TargetX, TargetXY.STY as TargetY, TargetZ
from @Result