IF OBJECT_ID (N'dbo.ZScaleUnits', N'FN') IS NOT NULL
    DROP FUNCTION ZScaleUnits;
GO
CREATE FUNCTION dbo.ZScaleUnits()
RETURNS varchar 
AS 
-- Returns the scale in the Z axis
BEGIN
    RETURN 'nm'
END