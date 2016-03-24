CREATE FUNCTION dbo.StructureLinkArea()
RETURNS @retStructureLinkArea TABLE
(
	SourceParentID bigint NOT NULL,
	SourceID bigint NOT NULL,
	TargetParentID  bigint NOT NULL,
	TargetID  bigint NOT NULL,
	SourceTypeID  bigint NOT NULL,
	TargetTypeID  bigint NOT NULL,
	Area_nm  float NOT NULL,
	shape  geometry NOT NULL
)
AS 
BEGIN
	declare @AreaScalar float
	set @AreaScalar = dbo.XYScale() * dbo.ZScale()
	IF OBJECT_ID('tempdb..#LinkPairs') IS NOT NULL DROP TABLE #LinkPairs
	IF OBJECT_ID('tempdb..#ExtraStructureLinks') IS NOT NULL DROP TABLE #ExtraStructureLinks
	IF OBJECT_ID('tempdb..#ContactDuplicates') IS NOT NULL DROP TABLE #ContactDuplicates
	IF OBJECT_ID('tempdb..#PairDistance') IS NOT NULL DROP TABLE #PairDistance
	IF OBJECT_ID('tempdb..#NearestPartner') IS NOT NULL DROP TABLE #NearestPartner
	IF OBJECT_ID('tempdb..#PairsToDelete') IS NOT NULL DROP TABLE #PairsToDelete

	select S.ID as SID, S.ParentID as SParentID, S.Radius as SRadius, S.MosaicShape as SMosaicShape, S.Z as SZ,
		   T.ID as TID, T.ParentID as TParentID, T.Radius as TRadius, T.MosaicShape as TMosaicShape, T.Z as TZ ,
		   S.MosaicShape.STDimension() as Dimension,
			case 
				when S.MosaicShape.STDimension() = 2 then
					dbo.ufnLineFromLinkedShapes(S.MosaicShape, T.MosaicShape)
				when S.MosaicShape.STDimension() = 1 then
					T.MosaicShape
				ELSE
					NULL
				end
			as Line
	INTO #LinkPairs
	from Location S 
	join StructureLink L ON L.SourceID = S.ParentID
	join Location T      ON L.TargetID = T.ParentID AND T.Z = S.Z
	order by S.ParentID, T.ParentID

	--Count the cases where we have two locations for the same structure on the same Z section that create extra fake contact patches.
	--NOTE: This code breaks if the Synapse overlaps with BOTH PSD pairs.  There's no fixing it.  Just ensure the synapse only overlaps with the correct PSD and not the more distance one.
	select SParentID, TParentID, SZ, NumPairs into #ExtraStructureLinks from (Select SParentID, TParentID, SZ, Count(SParentID) as NumPairs from #LinkPairs group by SParentID, TParentID, SZ) as ExtraStructureLinks where NumPairs > 1 order by SParentID

	select SL.SID, SL.TID, SMosaicShape.STDistance(TMosaicShape) as Distance into #PairDistance from #LinkPairs SL
	inner join #ExtraStructureLinks EL ON SL.SParentID = EL.SParentID AND SL.TParentID = EL.TParentID AND SL.SZ = EL.SZ

	select SL.SID, MIN(PD.Distance) as NearestPartnerDistance into #NearestPartner from #LinkPairs SL
	join #ExtraStructureLinks EL ON SL.SParentID = EL.SParentID AND SL.TParentID = EL.TParentID AND SL.SZ = EL.SZ
	join #PairDistance PD ON PD.SID = SL.SID AND PD.TID = SL.TID
	group by SL.SID

	--Remove the locations that have a nearer paired location
	delete SL from #LinkPairs SL
	inner join #PairDistance PD ON PD.SID = SL.SID AND PD.TID = SL.TID
	inner join #NearestPartner NP ON NP.SID = SL.SID
	where PD.Distance > NP.NearestPartnerDistance

	--Renders the first 1000 shapes in the spatial results view
	IF OBJECT_ID('tempdb..#GAggregate') IS NOT NULL DROP TABLE #GAggregate
	select SParentID as SParentID, SMosaicShape as Shape into #GAggregate from #LinkPairs where SMosaicShape is NOT NULL
	insert into #GAggregate (SParentID, Shape) Select SParentID, TMosaicShape from #LinkPairs where TMosaicShape is NOT NULL
	insert into #GAggregate (SParentID, Shape) Select SParentID, Line from #LinkPairs where Line IS NOT NULL

	--Measures the area of the PSD
	
	RETURN select S.ParentID as SourceParentID, SL.SParentID as SourceID, T.ParentID as TargetParentID, SL.TParentID as TargetID, S.TypeID as SourceTypeID, T.TypeID as TargetTypeID, sum( Line.STLength()) * @AreaScalar as Area_nm, geometry::CollectionAggregate(Shape) as shape from #StructureLinks SL
		join #GAggregate GA ON SL.SParentID = GA.SParentID
		join Structure S ON S.ID = SL.SParentID
		join Structure T ON T.ID = SL.TParentID
		group by SL.SParentID, SL.TParentID, S.ParentID, T.ParentID, S.TypeID, T.TypeID
		order by Area_nm
END