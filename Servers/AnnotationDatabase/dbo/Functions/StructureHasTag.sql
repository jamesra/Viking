
			CREATE FUNCTION StructureHasTag 
			(
				-- Add the parameters for the function here
				@StructureID bigint,
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
					FROM Structure
						cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
						WHERE ID = @StructureID)  
			END
		
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[StructureHasTag] TO PUBLIC
    AS [dbo];

