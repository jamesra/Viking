IF OBJECT_ID('tempdb..#GC_BC_Input') IS NOT NULL DROP TABLE #Aii_BC_Input

Select S.ID into #Aii_BC_Input from Location L 
inner join Structure S on S.ID = L.ParentID
WHERE z<=170

Select distinct S.ID, Child.ParentID from Structure S
inner join Structure Child ON Child.ParentID = S.ID
WHERE S.ID=514 AND 
dbo.StructureHasTag(Child.ID, N'Ribbon') = 1 AND 
Child.TypeID = 35 