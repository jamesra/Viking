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
    PRIMARY KEY ( ChildLocationID, ParentLocationID)
)
GO
