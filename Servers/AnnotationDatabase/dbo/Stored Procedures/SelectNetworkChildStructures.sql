
		    CREATE PROCEDURE [dbo].[SelectNetworkChildStructures]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				select S.* from Structure S 
					inner join NetworkChildStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END