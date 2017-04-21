if OBJECT_ID('tempdb..#Results') is not null
	DROP Table #Results 

DECLARE @Z float
set @Z = 20

DECLARE @IDs integer_list

--Cell ID's we want to test with each other
insert into @IDs (ID) values (476), (514)
 
DECLARE @MyCursor CURSOR;
DECLARE @MosaicShape Geometry;
DECLARE @StructureID bigint
BEGIN
    SET @MyCursor = CURSOR FOR
    select L.ParentID as StructureID, L.MosaicShape as MosaicShape from Location L
		inner join Structure S ON S.ID = L.ParentID 
		inner join @IDs ID on ID.ID = S.ID
		where L.Z=@Z
           
    OPEN @MyCursor 
    FETCH NEXT FROM @MyCursor 
    INTO @StructureID, @MosaicShape

	Create Table #Results (StructureID bigint, Distance float, NumAnnotations int)

    WHILE @@FETCH_STATUS = 0
    BEGIN 

	insert into #results select @StructureID as StructureID, Min(L.MosaicShape.STCentroid().STDistance(@MosaicShape.STCentroid())) * dbo.XYScale() as Distance_nm, count(L.ID)  from Location L 
		inner join @IDs ID ON ID.ID = L.ParentID
		where L.Z=@Z and L.ParentID != @StructureID
       
      FETCH NEXT FROM @MyCursor 
    INTO @StructureID, @MosaicShape
    END; 

    CLOSE @MyCursor ;
    DEALLOCATE @MyCursor;
END;

select * from #Results order by Distance
drop table #Results