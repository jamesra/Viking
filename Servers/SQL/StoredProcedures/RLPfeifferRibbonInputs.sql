
CREATE PROCEDURE RLPfeiffer.RibbonInputs @TargetCell bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.


IF OBJECT_ID('tempdb..#Rb_Input') IS NOT NULL DROP TABLE #Rb_Input

Select distinct S.ID as Parent, Child.ID as child into #Rb_Input from Structure S 
inner join Structure Child ON Child.ParentID = S.ID
WHERE
dbo.StructureHasTag(Child.ID, N'Ribbon') = 1 AND 
Child.TypeID = 35 


select SParent.ID as SourceParent, SParent.Label as SourceParentLabel, S.TypeID as SourceStructureType, SourceID, TargetID, TParent.ID as TargetParent, 
	TParent.Label as TargetParentLabel, SParent.Notes as Notes from StructureLink
inner join Structure T on T.ID = TargetID
inner join Structure S on S.ID = SourceID
inner join Structure TParent on T.ParentID = TParent.ID
inner join Structure SParent on S.ParentID =SParent.ID
where TargetID in (select Child from #Rb_Input)
AND TParent.ID = @TargetCell 
END
GO
