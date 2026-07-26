-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE FUNCTION ufnLinkedToLocations 
(	
	-- Add the parameters for the function here
	 @SourceLocIDs integer_list READONLY --The location IDs we are starting from
)
RETURNS @Links TABLE ( 
	SourceID bigint NOT NULL, --One of the Location IDs from @SourceLocIDs
	TargetID bigint NOT NULL,  --The ID of a linked location
	PRIMARY KEY CLUSTERED 
(
	[SourceID] ASC,
	[TargetID] ASC
) WITH (IGNORE_DUP_KEY = OFF),
	INDEX SourceID_idx NONCLUSTERED (SourceID asc), 
	INDEX TargetID_idx NONCLUSTERED (TargetID asc))
AS
BEGIN
	INSERT @Links
		select LL.B, LL.A from LocationLink LL
		inner join @SourceLocIDs B on B.ID = LL.B
		union
		select LL.A, LL.B from LocationLink LL
		inner join @SourceLocIDs A on A.ID = LL.A 
	RETURN
END
