IF OBJECT_ID (N'dbo.XYScaleUnits', N'FN') IS NOT NULL
    DROP FUNCTION XYScaleUnits;
GO
CREATE FUNCTION dbo.XYScaleUnits()
RETURNS varchar 
AS 
-- Returns the scale in the Z axis
BEGIN
    RETURN 'nm'
END