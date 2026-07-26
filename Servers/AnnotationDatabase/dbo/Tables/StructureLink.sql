CREATE TABLE [dbo].[StructureLink] (
    [SourceID]      BIGINT         NOT NULL,
    [TargetID]      BIGINT         NOT NULL,
    [Bidirectional] BIT            CONSTRAINT [DF_StructureLink_Bidirectional] DEFAULT ((0)) NOT NULL,
    [Tags]          XML            NULL,
    [Username]      NVARCHAR (254) CONSTRAINT [DF_StructureLink_Username] DEFAULT (N'') NOT NULL,
    [Created]       DATETIME       CONSTRAINT [DF_StructureLink_Created] DEFAULT (getutcdate()) NOT NULL,
    [LastModified]  DATETIME       CONSTRAINT [DF_StructureLink_LastModified] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [chk_StructureLink_Self] CHECK ([SourceID]<>[TargetID]),
    CONSTRAINT [FK_StructureLinkSource_StructureBaseID] FOREIGN KEY ([SourceID]) REFERENCES [dbo].[Structure] ([ID]),
    CONSTRAINT [FK_StructureLinkTarget_StructureBaseID] FOREIGN KEY ([TargetID]) REFERENCES [dbo].[Structure] ([ID]),
    CONSTRAINT [source_target_unique] UNIQUE NONCLUSTERED ([SourceID] ASC, [TargetID] ASC)
);


GO
CREATE NONCLUSTERED INDEX [SourceID]
    ON [dbo].[StructureLink]([SourceID] ASC)
    INCLUDE([TargetID]) WITH (FILLFACTOR = 90);


GO
CREATE NONCLUSTERED INDEX [TargetID]
    ON [dbo].[StructureLink]([TargetID] ASC)
    INCLUDE([SourceID]) WITH (FILLFACTOR = 90);


GO
CREATE TRIGGER [dbo].[StructureLink_ReciprocalCheck] 
		ON  [dbo].[StructureLink]
		AFTER INSERT, UPDATE
		AS 
			IF ((select count(SLA.SourceID)
				from inserted SLA 
				JOIN StructureLink SLB 
				ON (SLA.SourceID = SLB.TargetID AND SLA.TargetID = SLB.SourceID)) > 0)
				BEGIN
					RAISERROR('Reciprocal structure links are not allowed',14,1);
					ROLLBACK TRANSACTION;
					RETURN
				END
				
GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Last username to modify the row', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureLink', @level2type = N'COLUMN', @level2name = N'Username';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Row Creation Date', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StructureLink', @level2type = N'COLUMN', @level2name = N'Created';

