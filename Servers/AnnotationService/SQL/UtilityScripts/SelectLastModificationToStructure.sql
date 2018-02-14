select max(Q.LastModified) from (
	select max(L.LastModified) as LastModified from Location L where L.ParentID = 476
	union
	select max(LLA.Created) as LastModified from Location L 
		inner join LocationLink LLA ON LLA.A = L.ID
		where L.ParentID = 476
	union
	select max(S.LastModified) as LastModified from Structure S where S.ID = 476 or S.ParentID = 476
	) Q