CREATE FUNCTION dbo.XYScale()
										RETURNS float 
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the XY axis
										BEGIN
										RETURN 2.176 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[XYScale] TO PUBLIC
    AS [dbo];

