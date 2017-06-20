Select S.ID, S.N, S.V, 
		CASE WHEN S.N IS NULL THEN NULL
			 WHEN S.N = '' THEN NULL
			 WHEN S.V IS NULL THEN JSON_QUERY('{"' + S.N + '" : null }')			
			 WHEN TRY_CONVERT(float, S.V) IS NOT NULL THEN JSON_QUERY('{"' + S.N + '": '+ CONVERT(nvarchar(max),CONVERT(float,S.V)) + ' }') --Have to convert the value because JSON throws an error for numbers beginning with 0
			 ELSE JSON_QUERY('{"' + S.N + '": "'+ S.V + '" }') 
		END as Json,
		S.Tags
		
	from (

		select ID,
			   S.Tags as Tags, 
			   T2.Tags.value('./@Name', 'nvarchar(MAX)') as N,
			   T2.Tags.value('./@Value', 'nvarchar(MAX)') as V
		from Structure S
		CROSS APPLY Tags.nodes('Structure/Attrib') as T2(Tags)
		where S.Tags is not NULL 
	) S
	WHERE S.N IS NOT NULL AND S.N != ''

DECLARE @info NVARCHAR(MAX)
set @info = JSON_MODIFY('{}', '$.Glutamate', '')
PRINT @info

DECLARE @Val bigint
set @Val = 7
set @info = JSON_MODIFY(@info, 'append $.Complete', @Val)
PRINT @info
 
set @info = JSON_MODIFY(@info, 'append $.Complete', @Val)
PRINT @info 


DECLARE @info NVARCHAR(MAX)
set @info = JSON_MODIFY('{}', 'append $.Attributes', 'Glutamate')
PRINT @info

DECLARE @Val bigint
set @Val = 7
set @info = JSON_MODIFY(@info, 'append $.Attributes', @Val)
PRINT @info

set @info = JSON_MODIFY(@info, 'append $.Attributes', JSON_QUERY(N'{"NonSmokingMouse" : null}'))
PRINT @info
