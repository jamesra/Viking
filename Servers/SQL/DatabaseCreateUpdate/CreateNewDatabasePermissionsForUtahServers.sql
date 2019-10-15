select * from sys.sql_logins

/****** Object:  User [AD\OpR-Marc-VikingIdent]    Script Date: 8/9/2019 10:48:42 AM ******/
CREATE USER [AD\OpR-Marc-VikingIdent] FOR LOGIN [AD\OpR-Marc-VikingIdent] WITH DEFAULT_SCHEMA=[dbo]
GO


/****** Object:  User [AD\OpR-Marc-VikingTest]    Script Date: 8/9/2019 10:49:01 AM ******/
CREATE USER [AD\OpR-Marc-VikingTest] FOR LOGIN [AD\OpR-Marc-VikingTest] WITH DEFAULT_SCHEMA=[dbo]
GO


ALTER ROLE db_datareader ADD MEMBER [AD\OpR-Marc-VikingIdent]
ALTER ROLE db_datawriter ADD MEMBER [AD\OpR-Marc-VikingIdent]

GRANT EXEC TO [AD\OpR-Marc-VikingIdent]


ALTER ROLE db_datareader ADD MEMBER [AD\OpR-Marc-VikingTest]
ALTER ROLE db_datawriter ADD MEMBER [AD\OpR-Marc-VikingTest]

GRANT EXEC TO [AD\OpR-Marc-VikingTest]

CREATE USER [AD\OpR-MarcLab-All] FOR LOGIN [AD\OpR-MarcLab-All] WITH DEFAULT_SCHEMA=[dbo]
GO

GRANT EXEC To AnnotationPowerUser
GRANT SELECT To AnnotationPowerUser

ALTER ROLE db_datareader ADD MEMBER [AD\OpR-MarcLab-All]