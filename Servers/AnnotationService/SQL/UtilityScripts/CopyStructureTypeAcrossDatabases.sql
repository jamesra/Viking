USE RC2 
set identity_insert StructureType ON

insert into [RC2].dbo.StructureType
    (ID,	ParentID,	Name,	Notes,	MarkupType,	Tags,	StructureTags,	Abstract,	Color,
	 Code,	HotKey,	Username,	LastModified,	Created)
	select ID,	ParentID,	Name,	Notes,	MarkupType,	Tags,	StructureTags,	Abstract,	Color,
	 Code,	HotKey,	Username,	LastModified,	Created  from Rabbit.dbo.StructureType RC1ST
		where ID not in (Select ID from RC2.dbo.StructureType)


USE RC2 
set identity_insert StructureType OFF

