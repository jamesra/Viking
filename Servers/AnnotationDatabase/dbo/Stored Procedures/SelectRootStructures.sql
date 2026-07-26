
			 CREATE PROCEDURE [dbo].[SelectRootStructures]
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select * from Structure 
				where ParentID IS NULL
			 END