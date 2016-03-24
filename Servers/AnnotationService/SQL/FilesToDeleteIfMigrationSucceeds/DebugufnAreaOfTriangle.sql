declare @A geometry
declare @B geometry
declare @C geometry
declare @Area float

set @A = geometry::Point(1,1,0);
set @B = geometry::Point(1,1.5,0);
set @C = geometry::Point(2,1,0);

exec @Area = dbo.ufnTriangleArea @A, @B, @C
PRINT @Area