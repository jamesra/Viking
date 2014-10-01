
declare @TagName nvarchar(128)
set @TagName = 'Y+'

select ID
from Structure 
	cross apply Tags.nodes('Structure/Attrib/@Name') as T(N) 
	where 
	T.N.value('.', 'nvarchar(128)') = @TagName
	
