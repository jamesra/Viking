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
-- Description:	Takes the center of A, draws a line to 
-- the center of B.  B should be a closed shape.
-- Returns the first intersection along the line.
-- If the center of A is contained within B then 
-- the center of A is returned.
-- =============================================
CREATE FUNCTION ufnCenterToCenterIntersectionPoint
(
	-- Add the parameters for the function here
	@AShape geometry,
	@BShape geometry
)
RETURNS geometry
AS
BEGIN
	DECLARE @ACenter geometry
	DECLARE @BCenter geometry
	DECLARE @line    geometry
	DECLARE @intersections    geometry

	set @ACenter = @AShape.STCentroid()
	set @BCenter = @BShape.STCentroid()

	if( 1 = @BShape.STContains(@ACenter)  )
	BEGIN
		return @ACenter
	END
	
	set @line = dbo.ufnLineFromPoints(@ACenter, @BCenter)

	set @intersections = @BShape.STIntersection(@Line)
	return @intersections
END
GO

