USE TEST
GO

--------------------------- RIBBON update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape),
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vUnlinkedLocations UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where UL.TypeID = 73 AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
--------------------------- Gap Junction & PSD update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape) + PI() / 2.0,
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vUnlinkedLocations UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where (	 UL.TypeID = 28 OR -- Gap Junction
				 UL.TypeID = 35 OR -- PSD
				 UL.TypeID = 189 OR -- BC Conventional Synapse
				 UL.TypeID = 240 OR -- Plaque-like Pre
				 UL.TypeID = 241 OR -- Plaque-line Post
				 UL.TypeID = 85 ) --Adherens
			AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO

-------------------------------------------------------
--Begin migration code for locations part of a structure link, but without a partner on the same section
-------------------------------------------------------


--------------------------- RIBBON update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape),
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vLinkedLocationWithoutSectionPartner UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where UL.TypeID = 73 AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
--------------------------- Gap Junction & PSD update -----------------------------

update L SET TypeCode = 5, 
					MosaicShape = dbo.ufnLineFromAngleAndDistance(dbo.ufnAngleBetweenShapes(Ribbon.MosaicShape, Parent.MosaicShape) + PI() / 2.0,
									   Ribbon.Radius * 2,
									   Ribbon.MosaicShape.STCentroid())
	FROM Location L
		JOIN vLinkedLocationWithoutSectionPartner UL ON UL.ID = L.ID
		JOIN vNearestParentStructureLocation NP ON UL.ID = NP.ChildLocationID
		JOIN Location Ribbon ON Ribbon.ID = NP.ChildLocationID
		JOIN Location Parent ON Parent.ID = NP.ParentLocationID
	 where (	 UL.TypeID = 28 OR -- Gap Junction
				 UL.TypeID = 35 OR -- PSD
				 UL.TypeID = 189 OR -- BC Conventional Synapse
				 UL.TypeID = 240 OR -- Plaque-like Pre
				 UL.TypeID = 241 OR -- Plaque-line Post
				 UL.TypeID = 85 ) --Adherens
			AND L.TypeCode = 1 AND Ribbon.MosaicShape.STDimension() > 1 and Parent.MosaicShape.STDimension() > 1

GO
