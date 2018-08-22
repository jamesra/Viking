CREATE FUNCTION [dbo].[ufnLastStructureNetworkModification]
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
		select SL.LastModified as LastModified from Structure S 
			inner join StructureLink SL ON SL.SourceID = S.ID
			where S.ID = @ID
		union
		select TL.LastModified as LastModified from Structure S 
			inner join StructureLink TL ON TL.TargetID = S.ID
			where S.ID = @ID
		union
		select S.LastModified as LastModified from Structure S where S.ID = @ID
		) Q
		
	RETURN @ResultVar
END
