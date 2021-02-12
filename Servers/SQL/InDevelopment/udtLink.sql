USE [Test]
GO

/****** Object:  UserDefinedTableType [dbo].[integer_list]    Script Date: 12/4/2020 12:34:39 PM ******/
CREATE TYPE [dbo].[udtLinks] AS TABLE(
	[SourceID] [bigint] NOT NULL,
	[TargetID] [bigint] NOT NULL,
	PRIMARY KEY CLUSTERED 
(
	[SourceID] ASC,
	[TargetID] ASC
)WITH (IGNORE_DUP_KEY = OFF),
	INDEX SourceID_idx NONCLUSTERED (SourceID asc), 
	INDEX TargetID_idx NONCLUSTERED (TargetID asc)
)
GO


