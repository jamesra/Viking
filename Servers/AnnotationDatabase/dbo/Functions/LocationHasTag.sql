
			CREATE FUNCTION LocationHasTag 
			(
				-- Add the parameters for the function here
				@ID bigint,
				@TagName nvarchar(128)
			)
			RETURNS bit
			AS
			BEGIN
				-- Add the T-SQL statements to compute the return value here
				RETURN
					(SELECT MAX( CASE 
							WHEN N.value('.','nvarchar(128)') LIKE @Tagname THEN 1
							ELSE 0
						END)
						FROM Location
							cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
							WHERE ID = @ID) 
			END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[LocationHasTag] TO PUBLIC
    AS [dbo];

