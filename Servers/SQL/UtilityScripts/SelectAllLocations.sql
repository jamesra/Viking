SELECT [VolumeX]
       ,[VolumeY]
       ,[Z]
  FROM [Rabbit].[dbo].[Location] L
  INNER JOIN 
   (SELECT ID, TYPEID
	FROM Structure 
	WHERE TypeID = 73) J
  ON L.ParentID = J.ID
  ORDER BY Z
