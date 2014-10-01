USE [Rabbit]
GO
/****** Object:  StoredProcedure [dbo].[MergeStructures]    Script Date: 01/12/2011 14:27:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
Create PROCEDURE [dbo].[MergeStructures]
	-- Add the parameters for the stored procedure here
	@KeepStructureID bigint,
	@MergeStructureID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	update Location 
	set ParentID = @KeepStructureID 
	where ParentID = @MergeStructureID

	update Structure
	set ParentID = @KeepStructureID 
	where ParentID = @MergeStructureID
	
	update StructureLink
	set TargetID = @KeepStructureID
	where TargetID = @MergeStructureID
	
	update StructureLink
	set SourceID = @KeepStructureID
	where SourceID = @MergeStructureID

	Delete Structure
	where ID = @MergeStructureID
	
END
