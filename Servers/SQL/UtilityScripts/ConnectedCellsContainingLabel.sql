

/*
-- Gets the set of child structure IDs containing a label
select ID from Structure where ParentID in (
	Select ID from Structure where contains(Label, 'CbB4w')
	)
*/

/*
-- Get the structure IDs connected to cell with a certain label
select * from StructureLink where 
	SourceID in 
	(
		select ID from Structure where ParentID in 
		(
			Select ID from Structure where contains(Label, 'CbB4w')
		)
	)
	OR 
	TargetID in 
	( 
		select ID from Structure where ParentID in 
		(
			Select ID from Structure where contains(Label, 'CbB4w')
		)
	)
*/


/*
--All of the structures connected to a cell with a label containing a gap junction
select * from Structure where ID in
	(
		select SourceID as ID from StructureLink where BiDirectional  = 1 and 
			SourceID in 
			(
				select ID from Structure where ParentID in 
				(
					Select ID from Structure where contains(Label, 'CbB4w')
				)
			)
		UNION
		select TargetID as ID from StructureLink where Bidirectional = 1 and
			TargetID in 
			( 
				select ID from Structure where ParentID in 
				(
					Select ID from Structure where contains(Label, 'CbB4w')
				)
			)
	)
*/


DECLARE @LabelA as nvarchar(128)
DECLARE @LabelB as nvarchar(128)

SET @LabelA = ' "CbB4*" '
SET @LabelB = ' "CbB*" '


-- Display source cells
Select ID,Label from Structure where contains(Label, @LabelA)

-- Display posssible target cells
Select ID,Label from Structure where contains(Label, @LabelB)

--Show the target cells connected to source cells
select ID, Label, Tags from Structure where ID in 
	(
		select distinct ParentID from Structure where 
			TypeID = 28 AND /*Gap junction ID*/
			ID in
			(
				select SourceID as ID from StructureLink where BiDirectional  = 1 and 
					SourceID in 
					(
						select ID from Structure where ParentID in 
						(
							Select ID from Structure where contains(Label, @LabelA)
						)
					)
				UNION
				select TargetID as ID from StructureLink where Bidirectional = 1 and
					TargetID in 
					( 
						select ID from Structure where ParentID in 
						(
							Select ID from Structure where contains(Label, @LabelA)
						)
					)
			)
	) 
	AND contains(Label,@LabelB)