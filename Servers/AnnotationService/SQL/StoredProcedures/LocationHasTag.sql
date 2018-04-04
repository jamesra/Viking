-- ================================================
-- Template generated from Template Explorer using:
-- Create Scalar Function (New Menu).SQL
--
-- Use the Specify Values for Template Parameters 
-- command (Ctrl-Shift-M) to fill in the parameter 
-- values below.
--
-- This block of comments will not be included in
-- the definition of the function.
-- ================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION LocationHasTag 
(
	-- Add the parameters for the function here
	@ID bigint,
	@TagName nvarchar(128)
)
RETURNS bit
AS
BEGIN
	-- Add the T-SQL statements to compute the return value here
	RETURN
		(SELECT MAX( CASE 
				WHEN N.value('.','nvarchar(128)') LIKE @Tagname THEN 1
				ELSE 0
			END)
			FROM Location
				cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
				WHERE ID = @ID) 
END
GO

