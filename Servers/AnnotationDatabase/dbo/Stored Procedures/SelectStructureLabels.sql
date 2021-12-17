
			 CREATE PROCEDURE [dbo].[SelectStructureLabels]
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select ID, Label from Structure where Label is NOT NULL
			 END