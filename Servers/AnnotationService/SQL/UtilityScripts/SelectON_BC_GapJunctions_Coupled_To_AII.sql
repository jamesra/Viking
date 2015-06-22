/*
   Gap junction == 28 
   Conventional Synapse = 34
   Postsynapse = 35
   Ribbon Synapse = 73
   SELECT ID,Name from StructureType
*/ 

if OBJECT_ID('tempdb..#CB_IDs') is not null
	DROP Table #CB_IDs
if OBJECT_ID('tempdb..#Aii_IDs') is not null
	DROP Table #Aii_IDs
if OBJECT_ID('tempdb..#AII_GAPJUNCTIONS') is not null
	DROP Table #AII_GAPJUNCTIONS
if OBJECT_ID('tempdb..#CB_AII_RIBBON') is not null
	DROP Table #CB_AII_RIBBON
if OBJECT_ID('tempdb..#BC_AC_GAPJUNCTIONS') is not null
	DROP Table #BC_AC_GAPJUNCTIONS

 --- temp table exists


/*All Aii Structures */
select ID into #Aii_IDs from (Select ID from Structure where Label like '%Aii%') as ID


/*Select all cells with a gap junction involving an AII*/

	SELECT ID INTO #AII_GAPJUNCTIONS FROM Structure WHERE ID IN (
		/*Select all gap junctions involving Aii Amacrine cells*/
		SELECT TargetID FROM StructureLink WHERE SourceID IN (SELECT ID FROM Structure WHERE ParentID in (Select ID from #Aii_IDs) and TypeID = 28)
		UNION
		SELECT SourceID FROM StructureLink WHERE TargetID IN (SELECT ID FROM Structure WHERE ParentID in (Select ID from #Aii_IDs) and TypeID = 28)
		)  
	/*Select all bipolar cells with a ribbon contacting an AII*/
	SELECT ID INTO #CB_AII_RIBBON FROM Structure WHERE ID IN (
		/* Select all ribbon synapse structures from ON Bipolar onto AII amacrine cells */
		SELECT SourceID FROM StructureLink WHERE SourceID IN (SELECT ID FROM Structure WHERE ParentID in (Select ID from Structure where Label like '%CB%') AND TypeID = 73) AND
												 TargetID IN (SELECT ID FROM Structure WHERE ParentID in (Select ID from #Aii_IDs) AND TypeID = 35)
		)

/* Select the structure ID's of all the ON BC meeting criteria */
SELECT ParentID as ID INTO #CB_IDs from 
(
	SELECT ParentID from Structure where ID in (select ID From  #AII_GAPJUNCTIONS)
	INTERSECT 
	SELECT ParentID from Structure where ID in (select ID From  #CB_AII_RIBBON)
) as ID

/* Select the gap junction structures on the BC Side which connect to AII gap junctions*/
Select ID, ParentID INTO #BC_AC_GAPJUNCTIONS
 from Structure
 where (ParentID in (select * from #CB_IDs) and ID in (select * from #AII_GAPJUNCTIONS))  

/* Fetch the radius of gap junctions on the BC side and any relevant IDs */
select Location.ID as Loc_ID, Radius * 2.18 * 2 as Diameter, Location.ParentID as Struct_ID, #BC_AC_GAPJUNCTIONS.ParentID as Cell_ID from Location INNER JOIN #BC_AC_GAPJUNCTIONS
ON #BC_AC_GAPJUNCTIONS.ID=Location.ParentID where Location.ParentID in (select ID from #BC_AC_GAPJUNCTIONS)
order by Cell_ID, Struct_ID, Diameter 

GO
DROP Table #CB_IDs
GO
DROP Table #Aii_IDs
GO
DROP Table #AII_GAPJUNCTIONS
GO
DROP Table #CB_AII_RIBBON
GO
DROP table #BC_AC_GAPJUNCTIONS
GO