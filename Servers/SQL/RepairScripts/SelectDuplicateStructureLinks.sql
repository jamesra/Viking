select SLS.SourceID, SLS.TargetID, SLS.Bidirectional, SLS.Username, SLT.SourceID, SLT.TargetID, SLT.Bidirectional,  SLT.Username from StructureLink SLS 
inner join StructureLink SLT ON SLS.SourceID = SLT.TargetID 
Where SLS.TargetID = SLT.SourceID
order by SLS.SourceID