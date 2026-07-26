
			CREATE FUNCTION [dbo].[ufnLastStructureMorphologyModification]
			(
				-- Add the parameters for the function here
				@ID bigint
			)
			RETURNS DateTime
			AS
			BEGIN
				-- Declare the return variable here
				DECLARE @ResultVar DateTime

				-- Add the T-SQL statements to compute the return value here
				select @ResultVar = max(Q.LastModified) from (
					select L.LastModified as LastModified from Location L where L.ParentID = @ID
					union
					select LLA.Created as LastModified from Location L 
						inner join LocationLink LLA ON LLA.A = L.ID
						where L.ParentID = @ID
					union
					select S.LastModified as LastModified from Structure S where S.ID = @ID
					) Q
		
				RETURN @ResultVar
			END
		