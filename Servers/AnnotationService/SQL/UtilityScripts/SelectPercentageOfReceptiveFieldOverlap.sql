declare @SetA integer_list
declare @SetB integer_list

insert into @SetA select ID from Structure where Label = 'CBb5'
insert into @SetB select ID from Structure where Label = 'GAC AII'

Select CS.A_ID as AID, CS.B_ID as BID, SSVA.ConvexHull ConvexHullA, SSVB.ConvexHull ConvexHullB, SSVA.ConvexHull.STIntersection(SSVB.ConvexHull) as Intersection, SSVA.ConvexHull.STIntersection(SSVB.ConvexHull).STArea() / SSVA.ConvexHull.STArea() as Overlap
 from (select A.ID as A_ID, B.ID as B_ID from @SetA A, @SetB B) CS
	inner join StructureSpatialView SSVA ON SSVA.ID = CS.A_ID
	inner join StructureSpatialView SSVB ON SSVB.ID = CS.B_ID 
	WHERE SSVB.ConvexHull.STIntersects(SSVA.ConvexHull) = 1
