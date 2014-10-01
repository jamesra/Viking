SELECT IDList.ID FROM Location IDList
INNER JOIN
(
		SELECT L.ID
			   ,L.[ParentID]
			  ,L.[X]
			  ,L.[Y]
			  ,L.[Z]
			  ,L.[Radius]
			  ,J.TypeID
		  FROM [Rabbit].[dbo].[Location] L
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure
			WHERE Structure.ParentID IS NOT NULL) J
		  ON L.ParentID = J.ID
		INNER JOIN
		(SELECT L2.ID
			   ,L2.[ParentID]
			  ,[X]
			  ,[Y]
			  ,[Z]
			  ,[Radius]
			  ,J2.TypeID
		  FROM [Rabbit].[dbo].[Location] L2
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure
			WHERE Structure.ParentID IS NOT NULL) J2
		  ON L2.ParentID = J2.ID) N2
		ON N2.X = L.X AND 
		   N2.Y = L.Y AND
		   N2.Z = L.Z AND
		   (N2.ID = L.ID + 1 OR
			N2.ID = L.ID - 1) AND
		   (N2.ParentID = L.ParentID + 1 OR
			N2.ParentID = L.ParentID - 1)
		WHERE NOT EXISTS (SELECT * from LocationLink WHERE A = L.ID OR B = L.ID) 
	) ToDelete
	ON IDList.ID = ToDelete.ID