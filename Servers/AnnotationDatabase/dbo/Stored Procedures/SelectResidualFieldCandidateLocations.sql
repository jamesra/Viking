-- SelectResidualFieldCandidateLocations: procedure form of dbo.ResidualFieldCandidateLocations;
-- returns the same rows.
-- @MinLocations: minimum location count per structure; NULL or 0 means 3 (the default).
-- Callers: none in this repo; C# code queries dbo.ResidualFieldCandidateLocations directly.

		    CREATE PROCEDURE [dbo].[SelectResidualFieldCandidateLocations]
						@MinLocations int = 3
			AS
			BEGIN
				SET NOCOUNT ON;
				SELECT * FROM dbo.ResidualFieldCandidateLocations(@MinLocations);
			END
GO
