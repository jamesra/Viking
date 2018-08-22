USE [Rabbit]
GO
/****** Object:  StoredProcedure [dbo].[SelectStructuresTypesAndLinks]    Script Date: 01/12/2011 14:27:42 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
Create PROCEDURE [dbo].[SelectStructuresTypesAndLinks]
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	
	Select * from Structure
	
    -- Insert statements for procedure here
	Select * from StructureLink
	
END
