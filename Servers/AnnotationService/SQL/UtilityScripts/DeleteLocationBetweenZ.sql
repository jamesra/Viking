declare  @StartZ int
declare  @EndZ int

set @StartZ = 238
set @EndZ = 239

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