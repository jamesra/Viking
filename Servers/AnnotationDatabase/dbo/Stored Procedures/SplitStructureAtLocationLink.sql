   
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

				--Ensure that the location IDs of the keep and split locations are a location link.  Remove the link and continue;
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