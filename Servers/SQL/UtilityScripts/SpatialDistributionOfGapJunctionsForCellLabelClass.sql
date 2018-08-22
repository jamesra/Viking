select Max(Z) as Z, ParentID, geometry::ConvexHullAggregate( VolumeShape ) as CV from Location where ParentID in (
	select ID from Structure where TypeID = 28 and ParentID in (
		select ID from Structure where Label = 'CBb5w')
		) group by ParentID