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
-- Description:	<Description,,>
-- =============================================
GO
IF OBJECT_ID ( 'SelectStructureLocations', 'P' ) IS NOT NULL 
    DROP PROCEDURE SelectStructureLocations;
GO

CREATE PROCEDURE SelectStructureLocations
	-- Add the parameters for the stored procedure here
	@StructureID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT L.ID
       ,[ParentID]
      ,[VolumeX]
      ,[VolumeY]
      ,[Z]
      ,[Radius]
      ,J.TypeID
	  FROM [Rabbit].[dbo].[Location] L
	  INNER JOIN 
	   (SELECT ID, TYPEID
		FROM Structure
		WHERE ID = @StructureID OR ParentID = @StructureID) J
	  ON L.ParentID = J.ID
	  ORDER BY ID
END
GO
