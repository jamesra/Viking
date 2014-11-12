/*http://stackoverflow.com/questions/18932/how-can-i-remove-duplicate-rows/3822833#3822833*/
;WITH cte
     AS (SELECT ROW_NUMBER() OVER (PARTITION BY SourceID, TargetID
                                       ORDER BY ( SELECT 0)) RN
         FROM StructureLink)
delete from cte where RN > 1