CREATE TABLE [graph].[Structure] (
    [ID]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [TypeID]       BIGINT         NOT NULL,
    [Notes]        NVARCHAR (MAX) NULL,
    [Verified]     BIT            CONSTRAINT [DF_StructureBase_Verified] DEFAULT ((0)) NOT NULL,
    [Tags]         XML            NULL,
    [Confidence]   FLOAT (53)     CONSTRAINT [DF_StructureBase_Confidence] DEFAULT ((0.5)) NOT NULL,
    [Version]      ROWVERSION     NOT NULL,
    [ParentID]     BIGINT         NULL,
    [Created]      DATETIME       CONSTRAINT [DF_Structure_Created] DEFAULT (getutcdate()) NOT NULL,
    [Label]        VARCHAR (64)   NULL,
    [Username]     NVARCHAR (254) CONSTRAINT [DF_Structure_Username] DEFAULT (N'') NOT NULL,
    [LastModified] DATETIME       CONSTRAINT [DF_Structure_LastModified] DEFAULT (getutcdate()) NOT NULL,
    CONSTRAINT [PK_StructureBase] PRIMARY KEY CLUSTERED ([ID] ASC) WITH (FILLFACTOR = 90),
    CONSTRAINT [FK_Graph_Structure_Structure] FOREIGN KEY ([ParentID]) REFERENCES [graph].[Structure] ([ID]),
    CONSTRAINT [FK_Graph_StructureBase_StructureType] FOREIGN KEY ([TypeID]) REFERENCES [dbo].[StructureType] ([ID]),
    INDEX [GRAPH_UNIQUE_INDEX_2982623F05434AAC953721295B54E3CD] UNIQUE NONCLUSTERED ([$node_id])
) AS NODE;

