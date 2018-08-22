WITH tags as ( 
	select ID, TypeID, T.N.value('.', 'nvarchar(128)') as TagName
								from Structure cross apply Tags.nodes('Structure/Attrib/@Name') as T(N)
								)
SELECT TypeID, TagName, COUNT(TagName) as Count from tags group by TypeID, TagName order by TagName