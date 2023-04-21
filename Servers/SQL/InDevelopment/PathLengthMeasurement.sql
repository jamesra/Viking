SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#results', 'U') IS NOT NULL
  DROP TABLE #results;
   
CREATE TABLE #results (
	Code varchar(32) NOT NULL,
	SourceID bigint NOT NULL,
	TargetID bigint NOT NULL,
	Distance_um float NOT NULL,
	NumSteps bigint  NOT NULL
);

declare @Code varchar(32), @Source_ID bigint, @Target_ID bigint

--Probably should make the code below a function/procedure, but I need this working quick
DECLARE path_cursor CURSOR FOR
	SELECT Code, Start_ID, End_ID FROM [ExternalQueryData].dbo.RC1_3679;

OPEN path_cursor
FETCH NEXT FROM path_cursor INTO @Code, @Source_ID, @Target_ID

WHILE @@FETCH_STATUS = 0
BEGIN   
	declare @Target_IDs mem_integer_list
	insert into @Target_IDs (ID) VALUES (@Target_ID)

	IF OBJECT_ID('tempdb..#path', 'U') IS NOT NULL
	  DROP TABLE #path;

	IF OBJECT_ID('tempdb..#path_points', 'U') IS NOT NULL
	  DROP TABLE #path_points;
  
	-- PRINT CAST(@Source_ID AS VARCHAR) + ' -> ' + CAST(@Target_ID AS VARCHAR)
	
	BEGIN TRY
		select * into #Path from MorphologyPaths(@Source_ID, @Target_IDs)  
		--select * from #Path

		if OBJECT_ID('tempdb..#Path', 'U') IS NOT NULL and EXISTS (SELECT * FROM #Path)
		BEGIN

			declare @num_steps bigint
			select @num_steps = p.PathLength from #Path p

			--CANNOT USE integer_list, it sorts the keys
			select l.ID as ID, 
				l.VolumeX * dbo.XYScale() as VolumeX,
				l.VolumeY * dbo.XYScale() as VolumeY,
				l.Z * dbo.ZScale() as VolumeZ
				into #path_points 
				from (select s.value as ID from #Path p CROSS APPLY STRING_SPLIT (p.Path, ',') s) sp
				inner join Location L on L.ID = sp.ID

			--Uncomment to see the points we are using to calculate distance
			--select * from #path_points

			DECLARE @Distance float, @Step_Distance float
			set @Distance = 0 

			DECLARE @LastID bigint, @LastX float, @LastY float, @LastZ float
			DECLARE @ID bigint, @X float, @Y float, @Z float

			DECLARE point_cursor CURSOR FOR
				SELECT ID, VolumeX, VolumeY, VolumeZ FROM #path_points;

			OPEN point_cursor

			FETCH NEXT FROM point_cursor INTO @LastID, @LastX, @LastY, @LastZ
			IF @@FETCH_STATUS = 0
			BEGIN
				-- Fetch the next row from the cursor
				FETCH NEXT FROM point_cursor INTO @ID, @X, @Y, @Z

				WHILE @@FETCH_STATUS = 0
				BEGIN
		
					set @Step_Distance = dbo.ufnDistance3D(@LastX, @LastY, @LastZ, @X,@Y,@Z)
					set @Distance = @Distance + @Step_Distance
					-- Process the row
					--PRINT 'Step: ' + CAST(@ID AS VARCHAR) + ' -> ' + CAST(@LastID AS VARCHAR) + ', Step: ' + CAST(@Step_Distance AS VARCHAR) + ' Total: ' + CAST(@Distance as VARCHAR)

					set @LastX = @X
					set @LastY = @Y
					set @LastZ = @Z
					set @LastID = @ID

					FETCH NEXT FROM point_cursor INTO @ID, @X, @Y, @Z
				END
	 
			END
	
			CLOSE point_cursor;

			DEALLOCATE point_cursor;

			PRINT CAST(@Distance as VARCHAR) + 'nm'
			if @Distance = 0
				PRINT 'No Path from ' + CAST(@SOURCE_ID AS VARCHAR)  + ' -> ' + CAST(@Target_ID as VARCHAR)	    
			ELSE
				PRINT 'Path from ' + CAST(@SOURCE_ID AS VARCHAR)  + ' -> ' + CAST(@Target_ID as VARCHAR) + ' = ' + CAST(@Distance / 1000.0 as VARCHAR) + 'um'

			insert into #results (Code, SourceID, TargetID, Distance_um, NumSteps) values (@Code, @Source_ID, @Target_ID, @Distance / 1000.0, @num_steps)
			--select * from #results
		END
		ELSE BEGIN
			PRINT 'No Path from ' + CAST(@SOURCE_ID AS VARCHAR)  + ' -> ' + CAST(@Target_ID as VARCHAR)	    
			insert into #results (Code, SourceID, TargetID, Distance_um, NumSteps) values (@Code, @Source_ID, @Target_ID, 0, 0)
		END
	END TRY
	BEGIN CATCH
		PRINT 'Error measuring ' + CAST(@SOURCE_ID AS VARCHAR)  + ' -> ' + CAST(@Target_ID as VARCHAR) + ' : Number: ' + CAST(ERROR_NUMBER() as VARCHAR) + ' Line: ' + CAST(ERROR_LINE() as VARCHAR) + ' ' + ERROR_MESSAGE()
		insert into #results (Code, SourceID, TargetID, Distance_um, NumSteps) values (@Code, @Source_ID, @Target_ID, 0, -1) -- Use -1 to indicate error
	END CATCH

	FETCH NEXT FROM path_cursor INTO @Code, @Source_ID, @Target_ID

	delete from @Target_IDs
end

select * from #results
  
CLOSE path_cursor;

DEALLOCATE path_cursor;
