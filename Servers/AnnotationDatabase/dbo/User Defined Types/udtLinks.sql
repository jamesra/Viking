CREATE TYPE [dbo].[udtLinks] AS TABLE (
    [SourceID] BIGINT NOT NULL,
    [TargetID] BIGINT NOT NULL,
    PRIMARY KEY CLUSTERED ([SourceID] ASC, [TargetID] ASC),
    INDEX [TargetID_idx] ([TargetID]),
    INDEX [SourceID_idx] ([SourceID]));

