-- ResidualFieldCandidateLocations: every Location row whose structure (ParentID) has at least
-- @MinLocations locations; a lossy prefilter for residual-field section correction.
-- @MinLocations: minimum location count per structure; NULL or 0 means 3 (the default).
-- Callers: dbo.SelectResidualFieldCandidateLocations; AnnotationContext.ResidualFieldCandidateLocations
-- (ConnectomeDataModelCore); ConnectomeEntities.SelectResidualFieldCandidateLocations
-- (ConnectomeDataModel), used by the OData ResidualFieldLocations endpoint.

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
