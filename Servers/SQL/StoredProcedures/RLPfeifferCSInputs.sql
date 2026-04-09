IF OBJECT_ID('RLPfeiffer.CSInputs', 'p') IS NOT NULL
	DROP PROCEDURE RLPfeiffer.CSInputs;
GO

CREATE PROCEDURE RLPfeiffer.CSInputs @TargetCell bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.


IF OBJECT_ID('tempdb..#CS_Input') IS NOT NULL DROP TABLE #CS_Input

Select distinct S.ID as Parent, Child.ID as child into #CS_Input from Structure S 
inner join Structure Child ON Child.ParentID = S.ID
WHERE
dbo.StructureHasTag(Child.ID, N'Conventional') = 1 AND NOT dbo.StructureHasTag(Child.ID, N'Bipolar') = 1 AND
Child.TypeID = 35 

select SParent.ID as SourceParent, SParent.Label as SourceParentLabel, S.TypeID as SourceStructureType, SourceID, TargetID, TParent.ID as TargetParent, 
	TParent.Label as TargetParentLabel, SParent.Notes as Notes, dbo.ufnStructureArea(TargetID) as PSDArea from StructureLink
inner join Structure T on T.ID = TargetID
inner join Structure S on S.ID = SourceID
inner join Structure TParent on T.ParentID = TParent.ID
inner join Structure SParent on S.ParentID =SParent.ID
where TargetID in (select Child from #CS_Input)
AND TParent.ID = @TargetCell
ORDER BY SourceParentLabel
END
GO