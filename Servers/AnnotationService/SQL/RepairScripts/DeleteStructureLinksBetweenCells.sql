select SS.Label as SourceLabel, SS.TypeID as SourceType, TS.Label as TargetLabel, TS.TypeID as TargetType, SL.* from StructureLink SL
INNER JOIN Structure SS ON SS.ID = SL.SourceID
INNER JOIN Structure TS ON TS.ID = SL.TargetID
WHERE SS.ParentID IS NULL OR TS.ParentID IS NULL

delete SL from StructureLink SL
INNER JOIN Structure SS ON SS.ID = SL.SourceID
INNER JOIN Structure TS ON TS.ID = SL.TargetID
WHERE SS.ParentID IS NULL OR TS.ParentID IS NULL