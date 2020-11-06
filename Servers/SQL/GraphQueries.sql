
  select Origin, [Path], LastNode as Destination, levels
  from (
	select Source.ID as Origin,
		   STRING_AGG(dest.ID, ' -> ') WITHIN GROUP (GRAPH PATH) as [Path],
		   LAST_VALUE(dest.ID) WITHIN GROUP (GRAPH PATH) AS LastNode,
		   COUNT(dest.ID) WITHIN GROUP (GRAPH PATH) AS levels

	FROM
		graph.Location as Source,
		graph.LocationLink FOR PATH as link,
		graph.Location FOR PATH as Dest,
	inner join Structure S on S.ID = Source.ParentID
	WHERE MATCH(SHORTEST_PATH(Source(-(link)->Dest)+))
	and Source.ID = 5520 
	) as Q
where Q.LastNode = 5575


select count($from_id) from graph.StructureLink
select counT(SourceID) from StructureLink