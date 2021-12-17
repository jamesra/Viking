
			CREATE PROCEDURE [dbo].SelectStructureChangeLog
				-- Add the parameters for the stored procedure here
				@structure_ID bigint = NULL,
				@begin_time datetime = NULL,
				@end_time datetime = NULL 
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				DECLARE @capture_instance_name varchar(128)
				set @capture_instance_name = 'Structure'

				DECLARE @from_lsn binary(10), @to_lsn binary(10), @filter NVarChar(64)
				IF @begin_time IS NOT NULL
					set @from_lsn = sys.fn_cdc_map_time_to_lsn('smallest greater than', @begin_time)
				ELSE
					set @from_lsn  = sys.fn_cdc_get_min_lsn(@capture_instance_name)
	 
				IF @end_time IS NOT NULL
					set @to_lsn = sys.fn_cdc_map_time_to_lsn('largest less than or equal', @end_time)
				ELSE
					set @to_lsn  = sys.fn_cdc_get_max_lsn()
	 
				set @filter = N'all'

				if @structure_ID IS NOT NULL
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Structure(@from_lsn, @to_lsn, @filter) 
						where ID=@structure_ID 
						order by __$seqval
				ELSE 
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Structure(@from_lsn, @to_lsn, @filter) 
						order by __$seqval
			END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectStructureChangeLog] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectStructureChangeLog] TO [AnnotationPowerUser]
    AS [dbo];

