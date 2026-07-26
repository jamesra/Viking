
			CREATE PROCEDURE [dbo].[SelectStructureLocationLinksNoChildren]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				Select * from LocationLink
					 WHERE (A in 
						     (SELECT L.ID
							  FROM [Rabbit].[dbo].[Location] L
								INNER JOIN 
								(SELECT ID, TYPEID
									FROM Structure
									WHERE ID = @StructureID) J
								ON L.ParentID = J.ID
								)
							)
							OR
							(B in 
								(SELECT L.ID
								 FROM [Rabbit].[dbo].[Location] L
									INNER JOIN 
									(SELECT ID, TYPEID
										FROM Structure
										WHERE ID = @StructureID) J
									ON L.ParentID = J.ID
									)
							)
			END