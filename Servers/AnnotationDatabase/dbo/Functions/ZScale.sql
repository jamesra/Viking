CREATE FUNCTION dbo.ZScale()
										RETURNS float 
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the Z axis
										BEGIN
										RETURN 90 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ZScale] TO PUBLIC
    AS [dbo];

