
------Debug ufnWeightedMidpointBetweenCircles ----
declare @A1 geometry 
declare @A1Radius float
declare @A2 geometry
declare @A2Radius float

declare @CircleA1 geometry
declare @CircleA2 geometry

declare @MidpointA geometry
DECLARE @ParallelLineA geometry
DECLARE @PerpendicularLineA geometry

set @A1 = geometry::Point(1,4,0)
set @A1Radius = 4

--- Circle A1 and A2 intersect
set @CircleA1 = dbo.ufnCreateCircle(@A1, @A1Radius)

set @A2 = geometry::Point(2,0,0)
set @A2Radius = 2

set @CircleA2 = dbo.ufnCreateCircle(@A2, @A2Radius)
 
set @MidpointA = dbo.ufnWeightedMidpointBetweenCircles(@CircleA1, @CircleA2)
set @ParallelLineA = dbo.ufnParallelLineForLinkedShapes(@CircleA1, @CircleA2)
set @PerpendicularLineA = dbo.ufnPerpendicularLineForLinkedShapes(@CircleA1, @CircleA2)

--select @Circle1 as Circle1, @Circle2 as Circle2, @Midpoint as Midpoint, @IntersectingLine as IntersectingLine, @PerpendicularLine as PerpendicularLine
 
DECLARE @GeomAgg TABLE
(
shape geometry,
shapeType nvarchar(50)
);

INSERT INTO @GeomAgg Values (@CircleA1, '#A1'),
							(@CircleA2, '#A2'),
							(@MidpointA, 'MidpointA'),
							(@ParallelLineA, 'Parellel Migration Line A'),
							(@PerpendicularLineA, 'Perpendicular Migration Line A')



-- Circle B1 and B2 do not intersect

declare @CircleB1 geometry
declare @CircleB2 geometry

declare @MidpointB geometry
DECLARE @ParallelLineB geometry
DECLARE @PerpendicularLineB geometry

set @CircleB1 = dbo.ufnCreateCircle(geometry::Point(10,-1,0), 3)
set @CircleB2 = dbo.ufnCreateCircle(geometry::Point(11,5,0),2)
 
set @MidpointB = dbo.ufnWeightedMidpointBetweenCircles(@CircleB1, @CircleB2)
set @ParallelLineB = dbo.ufnParallelLineForLinkedShapes(@CircleB1, @CircleB2)
set @PerpendicularLineB = dbo.ufnPerpendicularLineForLinkedShapes(@CircleB1, @CircleB2)

INSERT INTO @GeomAgg Values (@CircleB1, '#B1'),
							(@CircleB2, '#B2'),
							(@MidpointB, 'MidpointB'),
							(@ParallelLineB, 'Parellel Migration Line B'),
							(@PerpendicularLineB, 'Perpendicular Migration Line B')

							
-- Circle C1 is contained in C2

declare @CircleC1 geometry
declare @CircleC2 geometry

declare @MidpointC geometry
DECLARE @ParallelLineC geometry
DECLARE @PerpendicularLineC geometry

set @CircleC1 = dbo.ufnCreateCircle(geometry::Point(0,14,0), 4)
set @CircleC2 = dbo.ufnCreateCircle(geometry::Point(-1,13,0),2)
 
set @MidpointC = dbo.ufnWeightedMidpointBetweenCircles(@CircleC1, @CircleC2)
set @ParallelLineC = dbo.ufnParallelLineForLinkedShapes(@CircleC1, @CircleC2)
set @PerpendicularLineC = dbo.ufnPerpendicularLineForLinkedShapes(@CircleC1, @CircleC2)



INSERT INTO @GeomAgg Values (@CircleC1, '#C1'),
							(@CircleC2, '#C2'),
							(@MidpointC, 'MidpointC'),
							(@ParallelLineC, 'Parellel Migration Line C'),
							(@PerpendicularLineC, 'Perpendicular Migration Line C')

				
-- Circle D2 is contained in D1

declare @CircleD1 geometry
declare @CircleD2 geometry

declare @MidpointD geometry
DECLARE @ParallelLineD geometry
DECLARE @PerpendicularLineD geometry

set @CircleD1 = dbo.ufnCreateCircle(geometry::Point(10,14,0), 2)
set @CircleD2 = dbo.ufnCreateCircle(geometry::Point(9,13,0),4)
 
set @MidpointD = dbo.ufnWeightedMidpointBetweenCircles(@CircleD1, @CircleD2)
set @ParallelLineD = dbo.ufnParallelLineForLinkedShapes(@CircleD1, @CircleD2)
set @PerpendicularLineD = dbo.ufnPerpendicularLineForLinkedShapes(@CircleD1, @CircleD2)


INSERT INTO @GeomAgg Values (@CircleD1, '#D1'),
							(@CircleD2, '#D2'),
							(@MidpointD, 'MidpointD'),
							(@ParallelLineD, 'Parellel Migration Line D'),
							(@PerpendicularLineD, 'Perpendicular Migration Line D')

select * from @GeomAgg
