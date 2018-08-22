-- ================================================
-- Template generated from Template Explorer using:
-- Create Procedure (New Menu).SQL
--
-- Use the Specify Values for Template Parameters 
-- command (Ctrl-Shift-M) to fill in the parameter 
-- values below.
--
-- This block of comments will not be included in
-- the definition of the procedure.
-- ================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	Split the structure at the provided LocationLink.  Deletes the LocationLink in the process
-- =============================================
CREATE PROCEDURE SplitStructureAtLocationLink
	@LocationIDOfKeepStructure bigint,
	@LocationIDOfSplitStructure bigint,
	@SplitStructureID bigint OUTPUT
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	set @SplitStructureID = 0

	--Ensure that the location ID's of the keep and split locations are a location link.  Remove the link and continue;
	IF (0 = (select COUNT(A) from LocationLink where (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure) OR 
											  (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure)))
		THROW 50000, N'The Split and Keep Location IDs must be linked', 1;

	BEGIN TRANSACTION split

		Delete LocationLink where (A = @LocationIDOfKeepStructure AND B = @LocationIDOfSplitStructure) OR 
									   (B = @LocationIDOfKeepStructure AND A = @LocationIDOfSplitStructure)
		Exec SplitStructure @LocationIDOfSplitStructure, @SplitStructureID

		if(@@error <> 0)
		 begin
			ROLLBACK TRANSACTION 
			RETURN
		 end 

	COMMIT TRANSACTION split

END 
