
			CREATE FUNCTION dbo.XYScaleUnits()
			RETURNS varchar(MAX)
			AS 
			-- Returns the scale in the Z axis
			BEGIN
				RETURN 'nm'
			END
		