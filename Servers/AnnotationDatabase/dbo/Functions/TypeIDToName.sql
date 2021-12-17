
	 CREATE FUNCTION TypeIDToName
		(
			-- Add the parameters for the function here
			@ID bigint 
		)
		RETURNS nvarchar(128)
		BEGIN
			-- Declare the return variable here
			DECLARE @Retval nvarchar(128)

			-- Add the T-SQL statements to compute the return value here
			SELECT top 1 @Retval = Name from StructureType ST where ST.ID = @ID

			-- Return the result of the function
			RETURN @Retval

		END

GO
GRANT EXECUTE
    ON OBJECT::[dbo].[TypeIDToName] TO PUBLIC
    AS [dbo];

