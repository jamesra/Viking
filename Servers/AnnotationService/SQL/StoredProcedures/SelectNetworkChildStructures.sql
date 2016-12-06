USE [Rabbit]
GO
/****** Object:  StoredProcedure [dbo].[SelectNetworkStructures]    Script Date: 12/2/2016 1:04:52 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[SelectNetworkChildStructures]
			-- Add the parameters for the stored procedure here
			@IDs integer_list READONLY,
			@Hops int
AS
BEGIN
	select S.* from Structure S 
		inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
END
			