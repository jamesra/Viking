USE [Test]
GO

/*Replacing a user-defined-table is a pain because each stored procedure and UDT using the type
  must be updated before the update.  This script is an attempt to automate the update*/

DROP TYPE IF EXISTS [dbo].[integer_list_MO] 

IF NOT EXISTS (
  SELECT * 
  FROM sys.table_types
  WHERE name = 'integer_list' 
  AND is_memory_optimized = 1
) 
BEGIN
 
  CREATE TYPE dbo.[integer_list_MO] 
    AS TABLE ( 
      [ID] bigint NOT NULL 
	  PRIMARY KEY NONCLUSTERED
			(
				[ID] ASC
			)
		) 
    WITH (MEMORY_OPTIMIZED=ON);
 
  -- the switcheroo!
  EXEC sp_rename 'dbo.integer_list', 'z_integer_list';
  EXEC sp_rename 'dbo.integer_list_MO', 'integer_list';
 
  --refresh modules
  DECLARE @Refreshmodulescripts TABLE (script nvarchar(max));

  INSERT @Refreshmodulescripts (script)
  SELECT QUOTENAME(referencing_schema_name) + '.' + QUOTENAME(referencing_entity_name) as obj
  FROM sys.dm_sql_referencing_entities('dbo.integer_list', 'TYPE')

  SELECT * FROM @Refreshmodulescripts
  DECLARE @Updates TABLE (script nvarchar(max));
  
  DECLARE @c bigint
  DECLARE @object_name nvarchar(max)

  DECLARE cursor_for_types CURSOR SCROLL FOR
	SELECT R.script from @Refreshmodulescripts R
  OPEN cursor_for_types
  FETCH NEXT FROM cursor_for_types INTO @object_name
  
  WHILE @@FETCH_STATUS = 0  
  BEGIN
	SELECT @object_name as Script
	DECLARE @is_dependent bit
	DECLARE @dependent_upon TABLE (script nvarchar(max))
	INSERT @dependent_upon (script) Select (CASE WHEN RE.referenced_schema_name IS NULL THEN '[dbo]' ELSE RE.referenced_schema_name END) + '.' + QUOTENAME(RE.referenced_entity_name) as script FROM sys.dm_sql_referenced_entities(@object_name, 'OBJECT') RE WHERE RE.referenced_entity_name IS NOT NULL AND RE.referenced_schema_name IS NOT NULL
	
	SELECT script as Dependency from @dependent_upon

	SELECT @is_dependent = (SELECT (CASE WHEN COUNT(Dep.script) > 0 THEN 1 ELSE 0 END)
		FROM @dependent_upon Dep 
		inner join @Refreshmodulescripts R ON R.script = Dep.script)
	
	PRINT @object_name + ' is_dependent = ' + STR(@is_dependent)
	IF( @is_dependent = 0 )
	BEGIN
		DECLARE @SQL nvarchar(max)
		SET @SQL = 'EXEC sp_refreshsqlmodule ''' + @object_name + ''';'
		PRINT @SQL
		EXEC sp_executesql @SQL; 
		if(@@error = 0)
		begin
			PRINT N'Success!'
			DELETE FROM @Refreshmodulescripts WHERE script = @object_name
		end  
		
	END 

	FETCH NEXT FROM cursor_for_types 
	INTO @object_name 

	DELETE FROM @dependent_upon
	
	if(@@FETCH_STATUS != 0)
	BEGIN
		FETCH FIRST FROM cursor_for_types;
		SELECT @c = (Select COUNT(script) from @Refreshmodulescripts)
		PRINT 'Trying from beginning, ' + STR(@c)
	END
  END

 /*
  INSERT @Refreshmodulescripts (script)
  SELECT 'EXEC sp_refreshsqlmodule ''' + QUOTENAME(referencing_schema_name) + '.' + QUOTENAME(referencing_entity_name) + ''';'
  FROM sys.dm_sql_referencing_entities('dbo.integer_list', 'TYPE')  
 
  DECLARE @SQL NVARCHAR(MAX) = N'';
  SELECT @SQL = @SQL + script FROM @Refreshmodulescripts;
 
  PRINT @SQL
  --EXEC sp_executesql @SQL; 
  */

  
  DROP TYPE IF EXISTS [dbo].z_integer_list 
END
 

