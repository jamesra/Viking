
			CREATE PROCEDURE [dbo].[SelectNetworkStructureLinks]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops)) OR
							SL.TargetID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops))
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectNetworkStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectNetworkStructureLinks] TO [AnnotationPowerUser]
    AS [dbo];

