

			ALTER PROCEDURE [dbo].[RecursiveSelectChildStructureIDs]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY
			AS
			BEGIN 	 
				DECLARE @NumSeedStructures int
				DECLARE @SeedStructures integer_list
				DECLARE @ChildStructures integer_list 

				insert into @SeedStructures select ID from @IDs 

				select @NumSeedStructures=count(ID) from @SeedStructures

				while @NumSeedStructures > 0
				BEGIN
					DECLARE @NewChildStructures integer_list 
					insert into @NewChildStructures
						select distinct Child.ID from Structure Child
							inner join @SeedStructures Parents on Parents.ID = Child.ParentID

					delete from @SeedStructures
					insert into @SeedStructures select ID from @NewChildStructures
					select @NumSeedStructures=count(ID) from @SeedStructures

					insert into @ChildStructures select ID from @NewChildStructures
					delete from @NewChildStructures
				END

				select ID from @ChildStructures
			END
			