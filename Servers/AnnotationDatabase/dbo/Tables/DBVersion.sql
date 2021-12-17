CREATE TABLE [dbo].[DBVersion] (
    [DBVersionID]   INT           NOT NULL,
    [Description]   VARCHAR (255) NOT NULL,
    [ExecutionDate] DATETIME      NOT NULL,
    [UserID]        INT           NOT NULL
);

