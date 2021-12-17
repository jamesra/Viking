
			CREATE FUNCTION ufnLastStructureMorphologyModificationRecursive
			(
				-- Add the parameters for the function here
				@ID bigint
			)
			RETURNS DateTime
			AS
			BEGIN
				-- Declare the return variable here
				DECLARE @ResultVar DateTime

				select @ResultVar = max(dbo.ufnLastStructureModification(S.ID)) from Structure S where S.ID = @ID or S.ParentID = @ID
	 
				RETURN @ResultVar
			END
		