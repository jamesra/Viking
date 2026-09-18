
		    CREATE FUNCTION [dbo].[ResidualFieldCandidateLocations]
			(
				@MinLocations int = 3
			)
			RETURNS TABLE
			AS
			RETURN
			(
				SELECT L.*
				FROM dbo.Location AS L
				INNER JOIN
				(
					SELECT ParentID
					FROM dbo.Location
					GROUP BY ParentID
					HAVING COUNT(*) >= ISNULL(NULLIF(@MinLocations, 0), 3)
                ) AS C ON C.ParentID = L.ParentID
			)
GO
