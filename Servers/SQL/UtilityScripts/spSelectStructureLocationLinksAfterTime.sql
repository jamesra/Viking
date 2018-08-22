Declare @Z float
Declare @QueryDate datetime
set @Z = 5
set @QueryDate = '2010-04-09'

Select * from LocationLink
	 WHERE (((A in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z >= @Z)
	 )
	  AND
	  (B in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z <= @Z)
	 ))
	 OR
	 ((A in
	 (SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z <= @Z)
	 )
	  AND
	  (B in 
	(SELECT ID
	  FROM [Rabbit].[dbo].[Location]
	  WHERE Z >= @Z)
	 )))
	 AND Created >= @QueryDate