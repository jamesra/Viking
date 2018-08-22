IF OBJECT_ID (N'dbo.XYScale', N'FN') IS NOT NULL
    DROP FUNCTION XYScale;
GO
CREATE FUNCTION dbo.XYScale()
RETURNS float 
AS 
-- Returns the scale in the XY axis
BEGIN
    RETURN 2.176
END