DECLARE @StructureID int
Set @StructureID = 476

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
	WHERE ID = @StructureID OR ParentID = @StructureID) J
  ON L.ParentID = J.ID
  ORDER BY ID
