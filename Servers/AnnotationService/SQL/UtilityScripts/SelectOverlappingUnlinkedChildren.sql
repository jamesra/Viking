IF OBJECT_ID('tempdb..#OverlappingLocationPool') IS NOT NULL DROP TABLE #OverlappingLocationPool
IF OBJECT_ID('tempdb..#OverlappingStructurePool') IS NOT NULL DROP TABLE #OverlappingStructurePool

select L1.ID as L1_ID, L2.ID as L2_ID, L1.ParentID as L1_ParentID, L2.ParentID as L2_ParentID, L1.Z as Z 
	into #OverlappingLocationPool from Location L1
	JOIN Location L2 ON L2.Z = L1.Z AND L1.ParentID != L2.ParentID
	WHERE L1.ParentID != L2.ParentID AND L1.ID < L2.ID AND
		  L1.MosaicShape.STIntersects(L2.MosaicShape) = 1
		  
delete LP from #OverlappingLocationPool LP
join StructureLink SL ON SL.SourceID = L1_ParentID AND SL.TargetID = L2_ParentID

delete LP from #OverlappingLocationPool LP
join StructureLink SL ON SL.SourceID = L2_ParentID AND SL.TargetID = L1_ParentID

delete LP from #OverlappingLocationPool LP
	join Structure S1 ON LP.L1_ParentID = S1.ID
	JOIN Structure S2 ON LP.L2_ParentID = S2.ID
	WHERE S1.ParentID IS NOT NULL AND S2.ParentID IS NOT NULL AND
		NOT (S1.TypeID = 35 AND S2.TypeID = 35)

select * from #OverlappingLocationPool