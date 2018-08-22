DECLARE @TypeID int
DECLARE @User varchar(128)
Set @TypeID = 28
Set @User = 'robertmarc'


SELECT L.ID
       ,[ParentID]
      ,[VolumeX]
      ,[VolumeY]
      ,[Z]
      ,[Radius]
      ,J.TypeID
  FROM [Rabbit].[dbo].[Location] L
  INNER JOIN 
   (SELECT ID, TYPEID
	FROM Structure
	WHERE TypeID = @TypeID) J
  ON L.ParentID = J.ID
  ORDER BY ID
