--Selects data needed to plot the area of annotations along a given path--

--select ID from Structure S where S.[Label] = N'CBb3n'

--select L.ID from Location L where L.ParentID = 142 order by L.Z

declare @SourceID bigint
declare @TargetIDs integer_list

set @SourceID = 6743
insert into @TargetIDs values (1218096)

declare @Path NVarchar(max) 

set @Path = (select MP.[Path] from MorphologyPaths(@SourceID, @TargetIDs) MP)

select S.value as ID, L.Radius * L.Radius * dbo.XYScale() / 1000000 as Area from string_split(@Path, ',') S
	inner join Location L ON L.ID = S.value
	where L.Radius < 300 * dbo.XYScale()