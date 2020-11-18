--This is a function to generate a partition function for a volume based on existing annotations

DECLARE @ZPartitionFunction nvarchar(max) = 
    N'CREATE PARTITION FUNCTION IntegerPartitionFunction (bigint) 
    AS RANGE LEFT FOR VALUES (';  
DECLARE @i bigint = (Select MIN(L.Z) from Location L)
DECLARE @MaxZ bigint = (Select MAX(L.Z) from Location L)
DECLARE @NumPartitions bigint = 1000
DECLARE @Step bigint = CAST(CEILING(CAST(@MaxZ - @i as float) / CAST(@NumPartitions as float)) as bigint)
 

IF @STEP < 1
BEGIN
	set @STEP = 1
END

WHILE @i <= @MaxZ
BEGIN  
SET @ZPartitionFunction += CAST(@i as nvarchar(10)) + N', ';  
SET @i += @Step;  
END  
SET @ZPartitionFunction += CAST(@i as nvarchar(10)) + N');';  
EXEC sp_executesql @ZPartitionFunction;  
GO  