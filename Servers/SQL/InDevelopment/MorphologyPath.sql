
use [Test]

SET NOCOUNT ON

declare @SourceIDs  integer_list  --Origin of the search
declare @TargetIDs integer_list  --Nodes we consider endpoints for a path
declare @VisitedIDs integer_list --Nodes we've already visited and checked for a path

declare @Path TABLE(
	[StartID] [bigint] NOT NULL,   --First node of the path
	[Path] nvarchar(max) NOT NULL DEFAULT '',	     --csv of intermediate nodes along the path
	[PathLength] [bigint] NOT NULL DEFAULT 0, --number of entries in the path so far
	[EndID] [bigint] NOT NULL, -- where the path ends
	[Completed] bit NOT NULL DEFAULT 0,  -- true if the path connects from a node in @SourceIDs to a node in @TargetIDs
	PRIMARY KEY CLUSTERED      -- There could be N paths between start and end.  We only keep the shortest path.  This key ensures we do not have duplicate paths
		(
			[StartID] ASC,
			[EndID] ASC
		) WITH (IGNORE_DUP_KEY = OFF) 
)

insert into @SourceIDs values (7051)
insert into @TargetIDs values (8770) 
insert into @TargetIDs values (8592)

select * from @SourceIDs
select * from @TargetIDs


--Prepopulate the @Path Table
insert into @Path (StartID, EndID)
	select ID as StartID, ID as EndID from @SourceIDs

--insert into @Path Values (0, @SourceID)

/*VisitedIDs is shared among all paths being searched.  If multiple sources and targets are specified it needs to be made specific for each path being explored*/
insert into @VisitedIDs 
	select [EndID] from @Path P
	LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
	WHERE V.ID IS NULL

--BEGIN
  --select * from @Path 
  --select * from @VisitedIDs
  --select * from @NextStepOriginIDs
   
  while 0 = ANY ( Select Completed from @Path ) 
  BEGIN
	  declare @Options integer_list --Possible paths we can explore
	  declare @PathOptions udtLinks  
	  declare @NextStepOriginIDs integer_list
	
	  DELETE FROM @NextStepOriginIDs

	  INSERT INTO @NextStepOriginIDs (ID)
		select EndID from @Path P
		where P.Completed = 0
		 
      --select * from @NextStepOriginIDs

	  --select * from @VisitedIDs

	  DELETE FROM @PathOptions

	  INSERT INTO @PathOptions (SourceID, TargetID)
		SELECT SourceID as SourceID, TargetID as TargetID 
		from [dbo].ufnLinkedToLocations(@NextStepOriginIDs) ll
		LEFT join @VisitedIDs VI ON ll.TargetID = VI.ID
		WHERE VI.ID IS NULL

	  --select COUNT(VI.ID) as [Count] from @VisitedIDs VI

	  --select * FROM @PathOptions
		 
	  declare @OptionCount TABLE (
		NumOptions bigint NOT NULL,
		OriginID bigint
	  )

	  delete from @OptionCount
	  --set @numLinks = (select COUNT(SourceID) from @PathOptions group by TargetID)
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

	  /*IF (SELECT MAX(OC.NumOptions) FROM @OptionCount OC) <> 1
	  BEGIN
		  select * from @OptionCount
		  select * from @PathOptions
	  END*/

	  --TODO: Update the visited node list

	 declare @DeadPathOriginIDs integer_list
	 DELETE FROM @DeadPathOriginIDs --This can be added again when we don't need to list every dead end reached in the search
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
						 [Completed] = CASE WHEN (EndID IN (Select * from @TargetIDs)) THEN 1 ELSE 0 END
			   from @Path P
			   inner join @PathOptions PO ON PO.SourceID = P.EndID
			   inner join @OptionCount PC ON PC.OriginID = P.EndID
			   where PC.NumOptions = 1 AND
					 PC.OriginID = P.EndID
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
	 
	 IF EXISTS (Select OriginID from @OptionCount OC where OC.NumOptions > 1)
	 BEGIN
		
		 /*select * from @PathOptions
		
		 select * from @Path as P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1
			*/
		--Multiple options, insert a row into the path table for each option
		INSERT INTO @Path (StartID, [Path], EndID, PathLength, Completed )
			SELECT P.StartID as StartID,
				   CASE WHEN P.[PATH] = '' THEN STR(EndID) ELSE CONCAT([Path],',',STR(EndID)) END, --Path
				   PO.TargetID as EndID,
				   [PathLength] = [PathLength] + 1,
				   [Completed] = CASE WHEN (EndID IN (Select * from @TargetIDs)) THEN 1 ELSE 0 END
			from @Path P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1
			/*
		select * from @Path as P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1
			*/
		DELETE P FROM @Path AS P
			INNER JOIN @PathOptions PO ON PO.SourceID = P.EndID
			INNER JOIN @OptionCount PC ON PC.OriginID = P.EndID
			where PC.NumOptions > 1

		--select * from @Path as P

		/*
		DECLARE complex_cursor CURSOR FOR  
				SELECT *
				FROM @PathOptions AS PO  
				INNER JOIN @OptionCount PC ON PC.OriginID = PO.SourceID
				WHERE PC.NumOptions >= 1
			OPEN complex_cursor;  
			FETCH FROM complex_cursor;  
			DELETE FROM @Path
			WHERE CURRENT OF complex_cursor;  
			CLOSE complex_cursor;
			DEALLOCATE complex_cursor;  
		*/
		   
		--select * from @OptionCount
		DELETE FROM @PathOptions
			   WHERE SourceID IN (SELECT OriginID from @OptionCount OC WHERE OC.NumOptions > 1)

	    --select * FROM @PathOptions

	 END
	 
	--if (Select COUNT(StartID) from @Path where Completed = 1)  > 0
	--	 select * from @Path where Completed=1
	--select * from @PathOptions
	
	/*select distinct [EndID] from @Path P
		LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
		WHERE V.ID IS NULL
	  */ 
	 insert into @VisitedIDs
		select distinct [EndID] from @Path P
		LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
		WHERE V.ID IS NULL
	/*
	select distinct [EndID] from @Path P
		LEFT JOIN @VisitedIDs V ON P.[EndID] = V.ID
		WHERE V.ID IS NULL
	*/
	DELETE @NextStepOriginIDs

  END

  --Ensure the target node is added to the Path string
  UPDATE @Path set [Path] = CASE WHEN [PATH] = '' THEN STR(EndID) ELSE CONCAT([Path],',',STR(EndID)) END,
				   [PathLength] = [PathLength] + 1
			   from @Path P

  select * from @Path
--END

--select * from @Path order by StepNumber Asc
--select * from @VisitedIDs asc