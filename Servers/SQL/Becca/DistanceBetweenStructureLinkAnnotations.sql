DECLARE @MC_ADJACENT TABLE (
	ParentID BIGINT NOT NULL,
	ChildID BIGINT NOT NULL,
	PRIMARY KEY (ParentID, ChildID)
); 

INSERT INTO @MC_ADJACENT
Select distinct S.ID as ParentID, Child.ID as ChildID from Structure S 
inner join Structure Child ON Child.ParentID = S.ID
WHERE
dbo.StructureHasTag(Child.ID, N'Ribbon') = 1 AND
Child.TypeID = 267

DECLARE @Distance_Measure_Details TABLE (
	SourceParentID BIGINT,
	SourceParentLabel VARCHAR(1024),
	SourceStructureTypeID BIGINT,
	SourceID BIGINT,
	TargetID BIGINT,
	CombinedShape Geometry,
	XYDistance FLOAT, 
	ZDistance FLOAT,
	TZ FLOAT,
	SZ FLOAT,
	TargetParentID BIGINT,
	TargetParentLabel VARCHAR(1024),
	Notes VARCHAR(4096),
	PSDArea FLOAT
);

INSERT INTO @Distance_Measure_Details
select SParent.ID as SourceParentID,
	   SParent.Label as SourceParentLabel,
	   S.TypeID as SourceStructureTypeID,
	   SL.SourceID,
	   SL.TargetID,
	   SLoc.VolumeShape.STUnion(TLoc.VolumeShape) as CombinedShape,
	   SLoc.VolumeShape.STDistance(TLoc.VolumeShape) * dbo.XYScale() as XYDistance,
	   ABS(TLoc.Z - SLoc.Z) * dbo.ZScale() as ZDistance,
	   TLoc.Z as TZ,
	   SLoc.Z as SZ,
	   TParent.ID as TargetParentID, 
	   TParent.Label as TargetParentLabel,
	   SParent.Notes as Notes,
	   dbo.ufnStructureArea(TargetID) as PSDArea
	   from StructureLink SL
inner join Structure T on T.ID = SL.TargetID
inner join Structure S on S.ID = SL.SourceID
inner join Structure TParent on T.ParentID = TParent.ID
inner join Structure SParent on S.ParentID = SParent.ID 
inner join Location TLoc on TLoc.ParentID = SL.TargetID
inner join Location SLoc on SLoc.ParentID = SL.SourceID
inner join @MC_ADJACENT MCADJ on MCADJ.ChildID = SL.TargetID
ORDER BY SourceStructureTypeID 


DECLARE @Distance_Measure TABLE ( 
	SourceID BIGINT,
	TargetID BIGINT,
	Distance_nm FLOAT,
	PRIMARY KEY (SourceID, TargetID)
); 

insert into @Distance_Measure
select 
	DM.SourceID as SourceID,
	DM.TargetID as TargetID,
	MIN(SQRT((DM.XYDistance * DM.XYDistance) + (DM.ZDistance * DM.ZDistance))) as Distance_nm
FROM @Distance_Measure_Details DM
GROUP BY DM.SourceID, DM.TargetID
 

select DMD.SourceID,
	   DMD.TargetID,
	   DMD.SourceParentID,
	   DMD.SourceParentLabel,
	   SynapseTargetParentStructure.Label as SynapseTargetParentStructureLabel,
	   DMD.SourceStructureTypeID,
	   DMD.TargetParentID,
	   DMD.TargetParentLabel,
	   DM.Distance_nm as Distance_nm,
	   DMD.PSDArea,
	   SL.TargetID as SynapseTargetID,
	   SynapseTargetStructure.TypeID as SynapseTargetTypeID,
	   STST.Name as SynapseTargetTypeName, 
	   SynapseTargetParentStructure.TypeID as SynapseTargetParentStructureTypeID,
	   STPST.Name as SynapseTargetParentTypeName,
	   DMD.Notes,
	   DMD.CombinedShape
FROM @Distance_Measure_Details DMD
inner join @Distance_Measure DM ON DM.SourceID = DMD.SourceID AND DM.TargetID = DMD.TargetID
LEFT join StructureLink SL ON SL.SourceID = DMD.SourceID AND SL.TargetID <> DMD.TargetID
LEFT join Structure SynapseTargetStructure ON SynapseTargetStructure.ID = SL.TargetID
LEFT join StructureType STST ON STST.ID = SynapseTargetStructure.TypeID
LEFT join Structure SynapseTargetParentStructure ON SynapseTargetParentStructure.ID = SynapseTargetStructure.ParentID
LEFT join StructureType STPST ON STPST.ID = SynapseTargetParentStructure.TypeID
WHERE SQRT((DMD.XYDistance * DMD.XYDistance) + (DMD.ZDistance * DMD.ZDistance)) = DM.Distance_nm
