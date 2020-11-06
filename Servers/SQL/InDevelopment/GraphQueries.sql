
declare @SourceIDs integer_list
declare @TargetIDs integer_list
declare @ConnectingChildIDs integer_list

insert into @ConnectingChildIDs Select L1.ID from [dbo].[Location] L1 
					   inner join [dbo].Structure St on L1.ParentID = St.ID
					   where St.TypeID = 28 or St.TypeID = 34 or St.TypeID = 73 or St.TypeID = 85

insert into @SourceIDs Select L1.ID from [dbo].[Location] L1 
					   inner join [dbo].Structure St on L1.ParentID = St.ID
					   where St.TypeID = 28 or St.TypeID = 85

insert into  @TargetIDs Select L2.ID from [Location] L2 
					   inner join Structure S2 on S2.ID = L2.ParentID
					   where S2.TypeID = 34 or S2.TypeID = 73 or S2.TypeID=85
 
--select Origin, OriginStructureID, [Path], LastNode as Destination, TargetStruct.ID as DestinationStructureID, TargetStruct.ParentID as CellID, levels
--from (
select  SideA_L1.ID       as  SideA_L1_ID,
		SideA_L1.ParentID as  SideA_S1_ID
		--SideA_L2.ID       as  SideA_L2,
		--SideA_L2.ParentID as  SideA_S2,
		--STRING_AGG(SideA_L2.ID, ' -> ') WITHIN GROUP (GRAPH PATH) as [Path],
		--LAST_VALUE(SideA_L2.ID) WITHIN GROUP (GRAPH PATH) AS SideA_L2_LastNode
		--LAST_VALUE(SideA_L2.ParentID) WITHIN GROUP (GRAPH PATH) AS LastNodeStructureID,
		--COUNT(SideA_L2.ID) WITHIN GROUP (GRAPH PATH) AS levels
FROM
	graph.[Location]              as SideA_L1,
	graph.[Location]     FOR PATH as SideA_L1_2, --The end node, should be equal to SideA_L1, limitation of SQL
	graph.[LocationLink] FOR PATH as SideA_Link, 
	graph.[Location]     FOR PATH as SideA_L2,
	graph.[StructureAttachLocation] FOR PATH   as SideA_L1_SideB_L1,
	graph.[StructureAttachLocation] FOR PATH   as SideA_L2_SideB_L2,
	graph.LocationLink   FOR PATH as SideB_Link,
	graph.[Location]     FOR PATH as SideB_L1, 
	graph.[Location]     FOR PATH as SideB_L2
--inner join @ConnectingChildIDs SideA_L1_Candidates ON SideA_L1_Candidates.ID = SideA_L1.ID
/*inner join (Select * from @ConnectingChildIDs) as SideA_L2Candidates ON SideA_L2Candidates.ID = SideA_L2.ID
inner join (Select * from @ConnectingChildIDs) as SideB_L1Candidates ON SideB_L1Candidates.ID = SideB_L1.ID
inner join (Select * from @ConnectingChildIDs) as SideB_L2Candidates ON SideB_L2Candidates.ID = SideB_L2.ID*/
WHERE SideA_L1.ID in (Select * from @ConnectingChildIDs)
	  AND SideA_L1.ParentID = 125956
	  AND MATCH(SHORTEST_PATH(SideA_L1        (-(SideA_Link)->SideA_L2)+ ) AND
            SHORTEST_PATH(LAST_NODE(SideA_L2) (-(SideA_L2_SideB_L2)->SideB_L2){1,2}) AND
			SHORTEST_PATH(LAST_NODE(SideB_L2) (-(SideB_Link)       -> SideB_L1)+ )   AND
			SHORTEST_PATH(LAST_NODE(SideB_L1) (-(SideA_L1_SideB_L1)-> SideA_L1_2){1,2})  
			 )
			 -- AND SideA_L1-(SideA_L1_SideB_L1)->SideB_L1 AND
			 -- AND SideA_L2-(SideA_L2_SideB_L2)->SideB_L2 AND
			 --SHORTEST_PATH(SideB_L1((-(SideB_Link)->SideB_L2)+)->SideB_L2)
			 --) 
	--AND (LAST_VALUE(SideA_L2.ID) WITHIN GROUP (GRAPH PATH)).ID in (Select * from @ConnectingChildIDs) 
	AND 
	/*AND SideB_L1.ID in (Select * from @ConnectingChildIDs) 
	AND SideB_L2.ID in (Select * from @ConnectingChildIDs) 
	AND SideA_L1_SideB_L1.ToStructureID = SideA_L2_SideB_L2.ToStructureID 
	AND SideA_L1_SideB_L1.FromStructureID = SideA_L2_SideB_L2.FromStructureID */
--) as Q 
 
--select count($from_id) from graph.StructureLink
--select counT(SourceID) from StructureLink