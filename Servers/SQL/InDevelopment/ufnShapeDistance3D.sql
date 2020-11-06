-- ================================================
-- Template generated from Template Explorer using:
-- Create Scalar Function (New Menu).SQL
--
-- Use the Specify Values for Template Parameters 
-- command (Ctrl-Shift-M) to fill in the parameter 
-- values below.
--
-- This block of comments will not be included in
-- the definition of the function.
-- ================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	3D Distance between two shapes. 
--  this distance is measured from the nearest 
--  points on both shapes
-- =============================================
ALTER FUNCTION ufnShapeDistance3D 
(
	-- Add the parameters for the function here
	@AShape geometry,
	@AZ float,
	@BShape geometry,
	@BZ float
)
RETURNS FLOAT
AS
BEGIN
	DECLARE @XYDistance FLOAT
	DECLARE @ZDist FLOAT
	DECLARE @XYZ_Distance FLOAT
	set @ZDist = @AZ - @BZ
	set @ZDist = ABS(@ZDist) * [dbo].[ZScale]()
	set @XYDistance = @AShape.STDistance( @BShape ) * [dbo].[XYScale]()
	 
	set @XYZ_Distance = SQRT( POWER(@ZDist,2) + POWER(@XYDistance, 2) )

	return @XYZ_Distance
END
GO

