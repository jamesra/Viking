-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	3D Distance between two shapes. 
--  this distance is measured from the nearest 
--  points on both shapes
-- =============================================
CREATE FUNCTION ufnDistance3D 
(
	-- Add the parameters for the function here
	@AX float,
	@AY float,
	@AZ float,
	@BX float,
	@BY float,
	@BZ float
)
RETURNS FLOAT
AS
BEGIN
	DECLARE @XYDistanceSquared FLOAT
	DECLARE @ZDist FLOAT
	DECLARE @XYZ_Distance FLOAT
	set @ZDist = (@AZ - @BZ) * [dbo].ZScale()
	set @XYDistanceSquared = POWER((@AX - @BX) * [dbo].XYScale(), 2) + POWER((@AY - @BY) * [dbo].XYScale(), 2) 
	 
	set @XYZ_Distance = SQRT( POWER(@ZDist,2) + @XYDistanceSquared )

	return @XYZ_Distance
END
