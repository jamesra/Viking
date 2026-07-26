CREATE FUNCTION NetworkStructureIDs
			(
				-- Add the parameters for the function here
				@IDs integer_list READONLY,
				@Hops int
			)
			RETURNS @CellsInNetwork TABLE 
			(
				-- Add the column definitions for the TABLE variable here
				ID bigint PRIMARY KEY
			)
			AS
			BEGIN
				-- Fill the table variable with the rows for your result set
	
				DECLARE @HopSeedCells integer_list 

				insert into @HopSeedCells select ID from @IDs 
				insert into @CellsInNetwork select ID from @IDs 

				while @Hops > 0
				BEGIN
					DECLARE @HopSeedCellsChildStructures integer_list
					DECLARE @ChildStructurePartners integer_list
					DECLARE @HopCellsFound integer_list
		
					insert into @HopSeedCellsChildStructures
						select distinct Child.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @HopSeedCells Cells ON Cells.ID = Parent.ID
		
					insert into @ChildStructurePartners
						select distinct SL.TargetID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.SourceID
						UNION
						select distinct SL.SourceID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.TargetID
				 
					insert into @HopCellsFound 
						select distinct Parent.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @ChildStructurePartners Partners ON Partners.ID = Child.ID
						where Parent.ID not in (Select ID from @CellsInNetwork union select ID from @HopSeedCells)
		
					delete S from @HopSeedCells S
		
					insert into @HopSeedCells 
						select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)

					insert into @CellsInNetwork select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)
			 

					delete from @ChildStructurePartners
					delete from @HopCellsFound
			 
					set @Hops = @Hops - 1
				END 

				RETURN 
			END