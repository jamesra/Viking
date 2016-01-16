select SS.Label as SourceLabel, TS.Label as TargetLabel, SL.* from StructureLink SL
INNER JOIN Structure SS ON SS.ID = SL.SourceID
INNER JOIN Structure TS ON TS.ID = SL.TargetID
WHERE SS.ParentID IS NULL OR TS.ParentID IS NULL

