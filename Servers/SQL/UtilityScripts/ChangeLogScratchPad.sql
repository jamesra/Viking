declare @structure_ID bigint
declare @begin_time datetime
declare @end_time datetime

set @structure_ID = NULL
set @begin_time = NULL
set @end_time = NULL

exec [SelectStructureLocationChangeLog] @structure_ID, @begin_time, @end_time

DECLARE @capture_instance_name varchar(128)
set @capture_instance_name = 'Structure'

DECLARE @from_lsn binary(10), @to_lsn binary(10), @filter NVarChar(64) 
set @from_lsn  = sys.fn_cdc_get_min_lsn(@capture_instance_name)
set @to_lsn  = sys.fn_cdc_get_max_lsn()
set @filter = N'all'

SELECT * FROM cdc.fn_cdc_get_all_changes_Structure(@from_lsn, @to_lsn, @filter) 