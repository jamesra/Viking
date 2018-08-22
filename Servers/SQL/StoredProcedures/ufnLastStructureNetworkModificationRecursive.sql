Create FUNCTION [dbo].[ufnLastNetworkModification]
(
	-- Add the parameters for the function here
	@IDs integer_list READONLY,
	@Hops int
)
RETURNS DateTime
AS
BEGIN
	-- Declare the return variable here
	DECLARE @ResultVar DateTime
	declare @Network_IDs integer_list

	insert into @Network_IDs 
	select ID from NetworkStructureIDs ( @IDs, @Hops )
	union 
	select ID from NetworkChildStructureIDs( @IDs, @Hops)
	    
	declare @Result DateTime
 
	select @ResultVar = MAX(S.LastModified) from Structure S
						inner join @Network_IDs N on N.ID = S.ID

	RETURN @ResultVar
END
