declare @A geometry
declare @B geometry
declare @L geometry
DECLARE @Angle float

set @A = geometry::Point(0,0,0)
set @B = geometry::Point(1,.5,0)
set @Angle = ATN2( @B.STY - @A.STY, @B.STX - @A.STX)

PRINT STR(@Angle, 10, 8) 

set @L = dbo.ufnLineFromAngleAndDistance(@Angle, @A.STDistance(@B), @A)

IF OBJECT_ID('tempdb..#GAggregate') IS NOT NULL DROP TABLE #GAggregate
select @L as Shape into #GAggregate
insert into #GAggregate (Shape) Values (@A)
insert into #GAggregate (Shape) Values (@B)

select Shape.ToString(), Shape from #GAggregate