DELETE LocationLink WHERE
A IN (
		SELECT L.ID
		  FROM [Rabbit].[dbo].[Location] L
		  INNER JOIN 
		   (SELECT A, B
			FROM LocationLink) J
		  ON L.ID = J.A
		  WHERE L.ParentID = 992
		  AND Z > 59
		  
		  )
OR
B IN (SELECT L.ID
		  FROM [Rabbit].[dbo].[Location] L
		  INNER JOIN 
		   (SELECT A, B
			FROM LocationLink) J
		  ON L.ID = J.A
		  WHERE L.ParentID = 992
		  AND Z > 59
		  
		  )