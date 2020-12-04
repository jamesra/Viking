
declare @SourceID bigint		 --Origin of the search
declare @TargetIDs integer_list  --Nodes we consider endpoints for a path
declare @VisitedIDs integer_list --Nodes we've already visited and checked for a path

declare @Path TABLE(
	[StepNumber] [bigint] UNIQUE NOT NULL,
	[LocID] [bigint] UNIQUE NOT NULL
)

set @SourceID = 7051
insert into @TargetIDs values (8770)

insert into @Path Values (0, @SourceID)
insert into @VisitedIDs 
	select LocID from @Path P
	LEFT JOIN @VisitedIDs V ON P.[LocID] = V.ID
	WHERE V.ID IS NULL

declare @path_step_ids integer_list

insert into @path_step_ids values (@SourceID) 

BEGIN
  declare @Options integer_list --Possible paths we can explore
  
  Select SourceID, TargetID from [dbo].ufnLinkedToLocations(@path_step_ids)

END

select * from @Path order by StepNumber Asc
select * from @VisitedIDs