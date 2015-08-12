declare int @StartZ
declare int @EndZ

set @StartZ = 1318
set @EndZ = 1319

DELETE LocationLink WHERE
A IN (
		SELECT L.ID
		  FROM Location L
		  INNER JOIN 
		   (SELECT A, B
			FROM LocationLink) J
		  ON L.ID = J.A
		  WHERE 
			Z >= @StartZ and
			Z <= @EndZ
		  
		  )
OR
B IN (SELECT L.ID
		  FROM Location L
		  INNER JOIN 
		   (SELECT A, B
			FROM LocationLink) J
		  ON L.ID = J.A
		  WHERE
			Z >= @StartZ and
			Z <= @EndZ
		  )

DELETE Location WHERE Z >= @StartZ and Z <= @EndZ