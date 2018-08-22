SET IDENTITY_INSERT StructureType ON
ALTER TABLE [RC2].[dbo].[StructureType] NOCHECK CONSTRAINT FK_StructureType_StructureType

Go 
INSERT INTO [RC2].[dbo].[StructureType]
           ([ID],
           [ParentID]
           ,[Name]
           ,[Notes]
           ,[MarkupType]
           ,[Tags]
           ,[StructureTags]
           ,[Abstract]
           ,[Color]
           ,[Code]
           ,[HotKey]
           ,[Username]
           ,[LastModified]
           ,[Created])
     Select [ID]
		   ,[ParentID]
           ,[Name]
           ,[Notes]
           ,[MarkupType]
           ,[Tags]
           ,[StructureTags]
           ,[Abstract]
           ,[Color]
           ,[Code]
           ,[HotKey]
           ,[Username]
           ,[LastModified]
           ,[Created] FROM [Rabbit]..StructureType
GO

ALTER TABLE [RC2].[dbo].[StructureType] CHECK CONSTRAINT FK_StructureType_StructureType

SET IDENTITY_INSERT StructureType OFF
