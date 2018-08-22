USE [Test]
GO
/****** Object:  UserDefinedFunction [dbo].[NetworkStructureIDs]    Script Date: 12/2/2016 2:39:33 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
ALTER FUNCTION [dbo].[NetworkChildStructureIDs]
(
	-- Add the parameters for the function here
	@IDs integer_list READONLY,
	@Hops int
)
RETURNS @ChildStructuresInNetwork TABLE 
(
	-- Add the column definitions for the TABLE variable here
	ID bigint PRIMARY KEY
)
AS
BEGIN
	-- Fill the table variable with the rows for your result set
	DECLARE @ChildIDsInNetwork integer_list 
	 
	insert into @ChildIDsInNetwork 
		select ChildStruct.ID from Structure S
		inner join NetworkStructureIDs(@IDs, @Hops) N ON S.ID = N.ID
		inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

	insert into @ChildStructuresInNetwork 
		select SL.SourceID as ID from StructureLink SL
			where SL.SourceID in (Select ID from @ChildIDsInNetwork)
		UNION
		select SL.TargetID as ID from StructureLink SL
			where SL.TargetID in (Select ID from @ChildIDsInNetwork)

	RETURN
END
