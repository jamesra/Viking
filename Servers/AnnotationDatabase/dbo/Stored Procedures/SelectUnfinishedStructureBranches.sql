
			 CREATE PROCEDURE [SelectUnfinishedStructureBranches]
				@StructureID bigint
			 AS
			 BEGIN
			 SET NOCOUNT ON;
  
			 select ID from 
				(select LocationID, COUNT(LocationID) as NumLinks from 
					(
						select A as LocationID from LocationLink 
							where
							(A in (Select L.ID from Location L where L.ParentID = @StructureID))
						union ALL
							select B as LocationID from LocationLink 
							where
							(B in (Select L.ID from Location L where L.ParentID = @StructureID))
					) as LinkedIDs
					Group BY LocationID ) as AllLocationLinks
					INNER JOIN
						(SELECT ID, Terminal, OffEdge from Location where Terminal = 0 and OffEdge = 0) L
					ON AllLocationLinks.LocationID = L.ID
					
					where AllLocationLinks.NumLinks <= 1
				order by ID 
			  END