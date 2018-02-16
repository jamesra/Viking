/*There are four instances of the database name to be updated.*/

USE NeitzTemporalMonkey 
set identity_insert StructureType ON

insert into NeitzTemporalMonkey.dbo.StructureType
    (ID,	ParentID,	Name,	Notes,	MarkupType,	Tags,	StructureTags,	Abstract,	Color,
	 Code,	HotKey,	Username,	LastModified,	Created)
	select ID,	ParentID,	Name,	Notes,	MarkupType,	Tags,	StructureTags,	Abstract,	Color,
	 Code,	HotKey,	Username,	LastModified,	Created  from Rabbit.dbo.StructureType RC1ST
		where ID not in (Select ID from NeitzTemporalMonkey.dbo.StructureType)


USE NeitzTemporalMonkey 
set identity_insert StructureType OFF

select * from StructureType