IF OBJECT_ID (N'dbo.ZScale', N'FN') IS NOT NULL
    DROP FUNCTION ZScale;
GO
CREATE FUNCTION dbo.ZScale()
RETURNS float 
AS 
-- Returns the scale in the Z axis
BEGIN
    RETURN 90.0
END