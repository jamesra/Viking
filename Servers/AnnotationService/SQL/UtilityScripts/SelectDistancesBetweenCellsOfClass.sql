if OBJECT_ID('tempdb..#ClassFields') is not null
	DROP Table #ClassFields

if OBJECT_ID('tempdb..#Results') is not null
	DROP Table #Results 

DECLARE @Label varchar(128)
set @Label = 'CBb3'

DECLARE @Z float
set @Z = 30

select L.ParentID as StructureID, L.MosaicShape as MosaicShape into #ClassFields from Location L
inner join Structure S ON S.ID = L.ParentID 
where L.Z=30 and S.Label = @Label

DECLARE @MyCursor CURSOR;
DECLARE @MosaicShape Geometry;
DECLARE @StructureID bigint
BEGIN
    SET @MyCursor = CURSOR FOR
    select L.ParentID as StructureID, L.MosaicShape as MosaicShape from Location L
		inner join Structure S ON S.ID = L.ParentID 
		where L.Z=@Z and S.Label = @Label
           

    OPEN @MyCursor 
    FETCH NEXT FROM @MyCursor 
    INTO @StructureID, @MosaicShape

	Create Table #Results (StructureID bigint, Distance float)

    WHILE @@FETCH_STATUS = 0
    BEGIN 

	insert into #results select @StructureID as StructureID, Min(L.MosaicShape.STDistance(@MosaicShape)) * dbo.XYScale() as Distance_nm from Location L 
		inner join Structure S ON S.ID = L.ParentID 
		where L.Z=@Z and S.Label = @Label and L.ParentID != @StructureID
       
      FETCH NEXT FROM @MyCursor 
    INTO @StructureID, @MosaicShape
    END; 

    CLOSE @MyCursor ;
    DEALLOCATE @MyCursor;
END;

select * from #Results order by Distance

drop table #ClassFields
drop table #Results