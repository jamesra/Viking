
use [Test]

SET NOCOUNT ON

declare @SourceIDs  integer_list  --Origin of the search
declare @TargetIDs integer_list  --Nodes we consider endpoints for a path
declare @VisitedIDs integer_list --Nodes we've already visited and checked for a path

declare @Path TABLE(
	[StartID] [bigint] NOT NULL,				--First node of the path
	[Path] nvarchar(max) NOT NULL DEFAULT '',	--csv of intermediate nodes along the path
	[PathLength] [bigint] NOT NULL DEFAULT 0,	--number of entries in the path so far
	[EndID] [bigint] NOT NULL,					-- where the path ends
	[Completed] bit NOT NULL DEFAULT 0,			-- true if the path connects from a node in @SourceIDs to a node in @TargetIDs
	PRIMARY KEY CLUSTERED						-- There could be N paths between start and end.  We only keep the shortest path.  This key ensures we do not have duplicate paths
		(
			[StartID] ASC,
			[EndID] ASC,
			[Completed] ASC
		) WITH (IGNORE_DUP_KEY = OFF)
)

insert into @SourceIDs values (7051)
insert into @TargetIDs values (8770) 
insert into @TargetIDs values (8592) 

declare @TargetsRemaining integer_list					--Lists the targets a path has not been located for
insert into @TargetsRemaining select ID from @TargetIDs

--Prepopulate the @Path Table
insert into @Path (StartID, EndID)
	select ID as StartID, ID as EndID from @SourceIDs

--insert into @Path Values (0, @SourceID)

/*VisitedIDs is shared among all paths being searched.  If multiple sources are specified further development is required to make it specific for each path being explored*/
insert into @VisitedIDs 
	select [EndID] from @Path P
	LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
	WHERE V.ID IS NULL
	 
  
  /*****************************************************************************************
  --Search until we have no more remaining paths to check or we have no more targets to find
  *****************************************************************************************/ 
 
  while 0 = ANY ( Select Completed from @Path ) AND (Select COUNT(ID) from @TargetsRemaining) > 0
  BEGIN
	  declare @Options integer_list --Possible paths we can explore
	  declare @PathOptions udtLinks  
	  declare @NextStepOriginIDs integer_list
	
	  DELETE FROM @NextStepOriginIDs

	  -- Determine which nodes need to be checked for edges during this pass
	  INSERT INTO @NextStepOriginIDs (ID)
		select EndID from @Path P
		where P.Completed = 0

	  DELETE FROM @PathOptions

	  -- List the edges
	  INSERT INTO @PathOptions (SourceID, TargetID)
		SELECT SourceID as SourceID, TargetID as TargetID 
		from [dbo].ufnLinkedToLocations(@NextStepOriginIDs) ll
		LEFT join @VisitedIDs VI ON ll.TargetID = VI.ID
		WHERE VI.ID IS NULL

	  --select COUNT(VI.ID) as [Count] from @VisitedIDs VI

	  --select * FROM @PathOptions
		 
	  /**************************************************
	  -- Count the number of edges available to each path
	  **************************************************/
	  declare @OptionCount TABLE (
		NumOptions bigint NOT NULL,
		OriginID bigint
	  ) 
	  delete from @OptionCount
	   
	  insert into @OptionCount
	  --Todo: Handle NULL value being removed by COUNT operation
	      select COUNT(TargetID) as NumOptions, OriginID as OriginID
			from (
				select PO.TargetID as TargetID, P.EndID as OriginID
				from @Path P
				LEFT JOIN @PathOptions PO ON  PO.SourceID = P.EndID
				WHERE P.Completed = 0
				) P
			WHERE TargetID IS NOT NULL
			group by P.OriginID
		   union 
		   select 0, EndID FROM @Path P
			WHERE P.EndID NOT IN (Select distinct SourceID from @PathOptions) AND
				  P.Completed = 0
 
	 /*******************************************************************
	 -- Identify which paths have reached a dead end because there are 
	 -- no more remaining nodes to check
	 *******************************************************************/
	 declare @DeadPathOriginIDs integer_list
	 DELETE FROM @DeadPathOriginIDs --This line prevents listing  every dead end reached in the search, but commenting it is useful for debugging which paths were checked
	 insert into @DeadPathOriginIDs
		Select OriginID
		FROM @OptionCount OC
		where OC.NumOptions = 0

	 IF EXISTS (Select * FROM @DeadPathOriginIDs)
	 BEGIN
		PRINT N'Removing dead paths'
		--select ID as DeletedIDs from @DeadPathOriginIDs
		--select * from @Path P 
		--	inner join @DeadPathOriginIDs DP ON DP.ID = P.EndID

		delete P from @Path P
			inner join @DeadPathOriginIDs DP ON DP.ID = P.EndID

		--select * from @Path
	 END

	 /*********************************************************************
	 -- If there is only one option for a path then update the existing row 
	 *********************************************************************/
	  
	  IF EXISTS (Select OriginID from @OptionCount OC where OC.NumOptions = 1)
	  BEGIN
		--PRINT N'Trivial path with one option'

		/*select * from @Path as P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions = 1
		*/
		UPDATE @Path set [Path] = CASE WHEN [PATH] = '' THEN STR(EndID) ELSE CONCAT([Path],',',STR(EndID)) END,
						 [EndID] = PO.TargetID,
						 [PathLength] = [PathLength] + 1,
						 [Completed] = 0--CASE WHEN (EndID IN (Select * from @TargetIDs)) THEN 1 ELSE 0 END
			   from @Path P
			   inner join @PathOptions PO ON PO.SourceID = P.EndID
			   inner join @OptionCount PC ON PC.OriginID = P.EndID
			   where PC.NumOptions = 1 AND
					 PC.OriginID = P.EndID AND			 
					 P.Completed = 0
		/*
		select * from @Path as P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions = 1
			*/

		DELETE FROM @PathOptions
			   WHERE SourceID IN (SELECT SourceID from @OptionCount OC WHERE OC.NumOptions = 1)
		
		--select * FROM @PathOptions

		 
	 END
	 
	 /******************************************************************
	 --If there are multiple paths then insert a new row for each option 
	 --and delete the original row
	 ******************************************************************/
	 IF EXISTS (Select OriginID from @OptionCount OC where OC.NumOptions > 1)
	 BEGIN
		 
		--Multiple options, insert a row into the path table for each option
		INSERT INTO @Path (StartID, [Path], EndID, PathLength, Completed )
			SELECT P.StartID as StartID,
				   CASE WHEN P.[PATH] = '' THEN STR(EndID) ELSE CONCAT([Path],',',STR(EndID)) END, --Path
				   PO.TargetID as EndID,
				   [PathLength] = [PathLength] + 1,
				   [Completed] = 0--CASE WHEN (EndID IN (Select * from @TargetIDs)) THEN 1 ELSE 0 END
			from @Path P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1 AND P.Completed = 0
		 
		DELETE P FROM @Path AS P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1
			  
		DELETE FROM @PathOptions
			   WHERE SourceID IN (SELECT OriginID from @OptionCount OC WHERE OC.NumOptions > 1)
			    
	 END

	 /**********************************************
	 Check for any completed paths.  Create a new row for the completed path with the completion flag set.
	 This allows testing for more targets along the same path
	 ***********************************************/
	   
	 INSERT INTO @Path (StartID, [Path], EndID, PathLength, Completed )
			SELECT P.StartID as StartID,
				   CASE WHEN P.[PATH] = '' THEN STR(EndID) ELSE CONCAT([Path],',',STR(EndID)) END, --Update Path to include EndID
				   P.EndID as EndID,
				   [PathLength] = [PathLength] + 1,
				   [Completed] = 1
			from @Path P
			inner join @TargetsRemaining TR ON TR.ID = P.EndID
			WHERE P.Completed = 0
			 
	 --Remove any target nodes we have located
	 delete T FROM @TargetsRemaining T
		inner join @Path P ON P.EndID = T.ID

	 /**********************************************
	 Record which nodes we have visited
	 **********************************************/
	  
	 insert into @VisitedIDs
		select distinct [EndID] from @Path P
		LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
		WHERE V.ID IS NULL

	DELETE @NextStepOriginIDs

  END

  --Remove incomplete paths to handle the case where all targets were found
  delete P from @Path P
  where P.Completed = 0

  --Return all of the identified paths
  select * from @Path

--END

--select * from @Path order by StepNumber Asc
--select * from @VisitedIDs asc