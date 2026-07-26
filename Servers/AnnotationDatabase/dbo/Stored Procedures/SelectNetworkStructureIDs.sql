CREATE PROCEDURE [dbo].SelectNetworkStructureIDs
				-- Add the parameters for the stored procedure here
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				select N.ID as ID from NetworkStructureIDs(@IDs, @Hops) N
			END