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
-- Description:	<Description, ,>
-- =============================================
MODIFY FUNCTION ufnStructureArea
(
	-- Add the parameters for the function here
	@StructureID bigint
)
RETURNS float
AS
BEGIN
	declare @Area float
	declare @AreaScalar float
	--Measures the area of the PSD
	set @AreaScalar = dbo.XYScale() * dbo.ZScale()

	
	select top 1 @Area = sum(MosaicShape.STLength()) * @AreaScalar from Location 
	where ParentID = @StructureID
	group by ParentID
	 
  
	-- Return the result of the function
	RETURN @Area

END
GO

