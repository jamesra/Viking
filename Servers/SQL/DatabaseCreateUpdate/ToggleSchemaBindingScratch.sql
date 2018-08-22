
	if(not(exists(select (1) from DBVersion where DBVersionID = 65)))
	begin
     print N'Convert functions to SchemaBinding to enable indexed view'
	 BEGIN TRANSACTION sixtyfive
		
		IF OBJECT_ID (N'dbo.ufnStructureVolume', N'FN') IS NOT NULL
			DROP FUNCTION ufnStructureVolume;
		IF OBJECT_ID (N'dbo.ufnStructureArea', N'FN') IS NOT NULL
			DROP FUNCTION ufnStructureArea;

		declare @XYScale float
		set @XYScale = dbo.XYScale()
		declare @ufnXYScale as VARCHAR(MAX)
		set @ufnXYScale = CONCAT('ALTER FUNCTION dbo.XYScale()
										RETURNS float 
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the XY axis
										BEGIN
										RETURN ', @XYScale, ' END')
		EXEC(@ufnXYScale)

		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 

		declare @ZScale float
		set @ZScale = dbo.ZScale()
		declare @ufnZScale as VARCHAR(MAX)
		set @ufnZScale = CONCAT('ALTER FUNCTION dbo.ZScale()
										RETURNS float 
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the Z axis
										BEGIN
										RETURN ', @ZScale, ' END')
		EXEC(@ufnZScale)

		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 
		  
		EXEC('ALTER FUNCTION dbo.XYScaleUnits()
										RETURNS varchar 
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the XY axis
										BEGIN
										RETURN ''nm'' END')

		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 
		   
		EXEC('ALTER FUNCTION dbo.ZScaleUnits()
										RETURNS varchar(MAX)
										WITH SCHEMABINDING
										AS 
										-- Returns the scale in the Z axis
										BEGIN
										RETURN ''nm'' END')

		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 

		--ALTER TABLE Location DROP CONSTRAINT chk_Location_Width 
		EXEC(' CREATE FUNCTION [dbo].[ufnStructureVolume]
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

				select top 1 @Area = sum(MosaicShape.STArea()) * @AreaScalar from dbo.Location 
				where ParentID = @StructureID
				group by ParentID
	  
				-- Return the result of the function
				RETURN @Area 
			END ')
      
		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 

		 --ALTER TABLE Location DROP CONSTRAINT chk_Location_Width 
		EXEC('CREATE FUNCTION [dbo].[ufnStructureArea]
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

				END')
      
		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 

		 INSERT INTO DBVersion values (65, N'Convert functions to SchemaBinding to enable indexed view' ,getDate(),User_ID())

	 COMMIT TRANSACTION sixtyfive
	end