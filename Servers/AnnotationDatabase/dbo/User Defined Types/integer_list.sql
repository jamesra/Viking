CREATE TYPE [dbo].[integer_list] AS TABLE (
    [ID] BIGINT NOT NULL,
    PRIMARY KEY CLUSTERED ([ID] ASC));


GO
GRANT EXECUTE
    ON TYPE::[dbo].[integer_list] TO PUBLIC;


GO
GRANT VIEW DEFINITION
    ON TYPE::[dbo].[integer_list] TO PUBLIC;


GO
GRANT VIEW DEFINITION
    ON TYPE::[dbo].[integer_list] TO [AnnotationPowerUser];

