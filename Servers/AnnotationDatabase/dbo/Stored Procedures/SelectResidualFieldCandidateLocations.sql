
		    CREATE PROCEDURE [dbo].[SelectResidualFieldCandidateLocations]
						@MinLocations int = 3
			AS
			BEGIN
				SET NOCOUNT ON;
				SELECT * FROM dbo.ResidualFieldCandidateLocations(@MinLocations);
			END
GO
