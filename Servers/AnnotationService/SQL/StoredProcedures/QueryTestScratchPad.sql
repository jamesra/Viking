declare @bbox geometry
declare @Z float
declare @minX float
declare @minY float
declare @maxX float
declare @maxY float
declare @PolyString varchar(1024)

set @minX = 61000
set @minY = 58200
set @maxX = 61200
set @maxY = 58400

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
set @Z = 380
declare @Radius float
set @Radius = 110

--exec SelectSectionLocationLinks @Z, NULL

exec SelectSectionLocationsAndLinksInBounds @Z, @bbox,  @Radius, NULL

exec SelectSectionStructuresAndLinksInBounds @Z, @bbox, @Radius, NULL
IF OBJECT_ID('tempdb..#SectionLocations') IS NOT NULL DROP TABLE #SectionLocations

exec [SelectSectionLocationsAndLinks] @Z, NULL

exec SelectSectionStructuresInBounds @Z, @bbox, @Radius, NULL




select * from Location where (@bbox.STIntersects(VolumeShape) = 1) and @Z = Z

select * from #SectionLocations 


select distinct ParentID from Location where
				 (@bbox.STIntersects(VolumeShape) = 1) AND Z = @Z order by ParentID 
				 





-- select * from [dbo].BoundedSectionLocations(@bbox, @Z)







