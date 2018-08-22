
select L1.ID as AID, L2.ID as BID, L1.ParentID as ParentA, L2.ParentID as ParentB from LocationLink INNER JOIN
	Location as L1 ON L1.ID = A INNER JOIN
		Location as L2 ON L2.ID = B
where
	L1.ParentID <> L2.ParentID
order by AID Desc

delete from LocationLink where A in (select L1.ID from LocationLink INNER JOIN
											  Location as L1 ON L1.ID = A INNER JOIN
										      Location as L2 ON L2.ID = B
										      where
											  L1.ParentID <> L2.ParentID and L2.ID = B)