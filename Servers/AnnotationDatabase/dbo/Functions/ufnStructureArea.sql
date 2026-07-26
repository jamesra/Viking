CREATE FUNCTION [dbo].[ufnStructureArea]
				(
					-- Add the parameters for the function here
					@StructureID bigint
				)
				RETURNS float
				WITH SCHEMABINDING
				AS
				BEGIN
					declare @Area float
					declare @AreaScalar float
					--Measures the area of the PSD
					set @AreaScalar = dbo.XYScale() * dbo.ZScale()

	
					select top 1 @Area = sum(MosaicShape.STLength()) * @AreaScalar from dbo.Location 
					where ParentID = @StructureID
					group by ParentID
	  
					-- Return the result of the function
					RETURN @Area

				END