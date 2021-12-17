
		    CREATE PROCEDURE [dbo].[SelectNetworkStructures]
				-- Add the parameters for the stored procedure here
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				select S.* from Structure S 
					inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNetworkStructures] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNetworkStructures] TO [AnnotationPowerUser]
    AS [dbo];

