CREATE FULLTEXT INDEX ON [dbo].[Location]
    ([Username] LANGUAGE 1033)
    KEY INDEX [PK_Location]
    ON [FullTextCatalog];


GO
CREATE FULLTEXT INDEX ON [dbo].[Structure]
    ([Label] LANGUAGE 1033, [Username] LANGUAGE 1033)
    KEY INDEX [PK_StructureBase]
    ON [FullTextCatalog];


GO
CREATE FULLTEXT INDEX ON [dbo].[StructureType]
    KEY INDEX [PK_StructureType]
    ON [FullTextCatalog];


GO
ALTER FULLTEXT INDEX ON [dbo].[StructureType] DISABLE;

