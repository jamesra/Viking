CREATE TYPE [dbo].[udtParentChildIDMap] AS TABLE (
    [ID]       BIGINT NOT NULL,
    [ParentID] BIGINT NOT NULL,
    INDEX [udtParentChildIDMap_ParentID_idx] ([ParentID]),
    INDEX [udtParentChildIDMap_idx1] ([ID]));

