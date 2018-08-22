declare @bbox geometry
declare @Z float
declare @minX float
declare @minY float
declare @maxX float
declare @maxY float
declare @PolyString varchar(1024)
declare @LastModified datetime

set @LastModified = '1-1-2015'

set @minX = 40000
set @minY = 40000
set @maxX = 70000
set @maxY = 70000

set @PolyString = 'POLYGON (( ' + STR(@minX) + ' ' + STR(@minY) + ', ' + 
						 STR(@maxX) + ' ' + STR(@minY) + ', ' + 
						 STR(@maxX) + ' ' + STR(@maxY) + ', ' + 
						 STR(@minX) + ' ' + STR(@maxY) + ', ' + 
						 STR(@minX) + ' ' + STR(@minY) + '))'

print @PolyString
 
set @bbox = 'POLYGON (( ' + STR(@minX) + ' ' + STR(@minY) + ', ' + 
						 STR(@maxX) + ' ' + STR(@minY) + ', ' + 
						 STR(@maxX) + ' ' + STR(@maxY) + ', ' + 
						 STR(@minX) + ' ' + STR(@maxY) + ', ' + 
						 STR(@minX) + ' ' + STR(@minY) + '))'
set @Z = 250
declare @Radius float
set @Radius = 110

--exec SelectSectionLocationLinks @Z, NULL

--exec SelectSectionLocationsAndLinksInVolumeBounds @Z, @bbox,  @Radius, NULL
--exec SelectSectionStructuresAndLinksInVolumeBounds @Z, @bbox, @Radius, @LastModified
--exec SelectSectionStructuresAndLinksInVolumeBounds2 @Z, @bbox, @Radius, NULL

--exec SelectSectionAnnotationsInBounds @Z, @bbox, @Radius, @LastModified
exec SelectSectionAnnotationsInBounds @Z, @bbox, @Radius, NULL

/*
IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations

exec [SelectSectionLocationsAndLinks] @Z, NULL

exec SelectSectionStructuresInBounds @Z, @bbox, @Radius, NULL




select * from Location where (@bbox.STIntersects(VolumeShape) = 1) and @Z = Z

select * from #SectionLocations 


select distinct ParentID from Location where
				 (@bbox.STIntersects(VolumeShape) = 1) AND Z = @Z order by ParentID 
				 





-- select * from [dbo].BoundedSectionLocations(@bbox, @Z)




*/


