-- ================================
-- Create User-defined Table Type
-- ================================ 
-- Create the data type
CREATE TYPE [dbo].[udtParentChildLocationPairs] AS TABLE 
(
	ChildLocationID bigint NOT NULL,
	ChildStructureID bigint NOT NULL,
	ParentLocationID bigint NOT NULL,
	ParentStructureID bigint NOT NULL
    PRIMARY KEY ( ChildLocationID ASC, ParentLocationID ASC)WITH (IGNORE_DUP_KEY = OFF),
	INDEX ChildLocationID_idx NONCLUSTERED (ChildLocationID asc),
	INDEX ChildStructureID_idx NONCLUSTERED (ChildStructureID asc), 
	INDEX ParentLocationID_idx NONCLUSTERED (ParentLocationID asc), 
	INDEX ParentStructureID_idx NONCLUSTERED (ParentStructureID asc)
)
GO
