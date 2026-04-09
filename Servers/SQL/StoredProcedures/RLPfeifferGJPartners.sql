IF OBJECT_ID('RLPfeiffer.GJPartners', 'p') IS NOT NULL
	DROP PROCEDURE RLPfeiffer.GJPartners;
GO

CREATE PROCEDURE RLPfeiffer.GJPartners @TargetCell bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.

IF OBJECT_ID('tempdb..#GJPartner') IS NOT NULL DROP TABLE #GJPartner

Select distinct S.ID as Parent, Child.ID as child into #GJPartner from Structure S 
inner join Structure Child ON Child.ParentID = S.ID
WHERE
Child.TypeID = 28 



select SParent.ID as SourceParent, SParent.Label as SourceParentLabel, S.TypeID as SourceStructureType, SourceID,
	   TargetID, TParent.ID as TargetParent, 	TParent.Label as TargetParentLabel, SParent.Notes as Notes
from StructureLink
	inner join Structure T on T.ID = TargetID
	inner join Structure S on S.ID = SourceID
	inner join Structure TParent on T.ParentID = TParent.ID
	inner join Structure SParent on S.ParentID = SParent.ID
	inner join #GJPartner GJP on GJP.child = TargetID
WHERE TParent.ID = @TargetCell OR SParent.ID = @TargetCell
ORDER BY SourceParentLabel
END
GO