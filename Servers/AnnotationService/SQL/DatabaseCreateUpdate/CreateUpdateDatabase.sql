/**** You need to replace the following templates to use this script ****/
/**** TEST = Name of the database  */
/**** {DATABASE_DIRECTORY} = Directory Datbase lives in if it needs to be created, with the trailing slash i.e. C:\Database\
*/
DECLARE @DATABASE_NAME VARCHAR(50)
SET @DATABASE_NAME = 'TEST'
DECLARE @DATABASE_DIRECTORY VARCHAR(50)
SET @DATABASE_DIRECTORY = 'C:\Database\'

USE [master]

/* Create the standard user accounts if they do not exist */
IF NOT EXISTS(SELECT name FROM sys.sql_logins WHERE name = 'security')
BEGIN
    CREATE LOGIN [security] WITH PASSWORD = 'hello123'   
END

/* Create the standard user accounts if they do not exist */
IF NOT EXISTS(SELECT name FROM sys.sql_logins WHERE name = 'Matlab')
BEGIN
    CREATE LOGIN [Matlab] WITH PASSWORD = '4%w%o06'   
END

/* Create the database in its initial conifiguration if it doesn't exist */

IF OBJECT_ID(N'tempdb..#UpdateVars', N'U') IS NOT NULL 
BEGIN
	DROP TABLE #UpdateVars;
END
	
CREATE TABLE #UpdateVars ([Version] VARCHAR(100));
INSERT INTO #UpdateVars Values (N'TEST');

DECLARE @db_id VARCHAR(100);
SET @db_id = db_id(@DATABASE_NAME)

print @db_id


IF @db_id IS NULL
BEGIN
	print N'Database does not exist, creating...' 
	
	declare @Path varchar(100)
	set @Path = N'C:\Database\TEST\'
	EXEC master.dbo.xp_create_subdir @Path
	
	/****** Object:  Database [TEST]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE DATABASE [TEST] ON  PRIMARY 
		( NAME = N'TEST', FILENAME = N'C:\Database\TEST\TEST.mdf' , SIZE = 4096KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
		 LOG ON 
		( NAME = N'TEST_log', FILENAME = N'C:\Database\TEST\TEST_log.ldf' , SIZE = 4096KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
		
	ALTER DATABASE [TEST] SET COMPATIBILITY_LEVEL = 100
	
	IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
	begin
		EXEC [TEST].[dbo].[sp_fulltext_database] @action = 'enable'
	end
	
	ALTER DATABASE [TEST] SET ANSI_NULL_DEFAULT OFF
	ALTER DATABASE [TEST] SET ANSI_NULLS OFF
	ALTER DATABASE [TEST] SET ANSI_PADDING OFF
	ALTER DATABASE [TEST] SET ANSI_WARNINGS OFF
	ALTER DATABASE [TEST] SET ARITHABORT OFF
	ALTER DATABASE [TEST] SET AUTO_CLOSE OFF
	ALTER DATABASE [TEST] SET AUTO_CREATE_STATISTICS ON
	ALTER DATABASE [TEST] SET AUTO_SHRINK OFF
	ALTER DATABASE [TEST] SET AUTO_UPDATE_STATISTICS ON
	ALTER DATABASE [TEST] SET CURSOR_CLOSE_ON_COMMIT OFF
	ALTER DATABASE [TEST] SET CURSOR_DEFAULT  GLOBAL
	ALTER DATABASE [TEST] SET CONCAT_NULL_YIELDS_NULL OFF
	ALTER DATABASE [TEST] SET NUMERIC_ROUNDABORT OFF
	ALTER DATABASE [TEST] SET QUOTED_IDENTIFIER OFF
	ALTER DATABASE [TEST] SET RECURSIVE_TRIGGERS OFF
	ALTER DATABASE [TEST] SET  DISABLE_BROKER
	ALTER DATABASE [TEST] SET AUTO_UPDATE_STATISTICS_ASYNC OFF
	ALTER DATABASE [TEST] SET DATE_CORRELATION_OPTIMIZATION OFF
	ALTER DATABASE [TEST] SET TRUSTWORTHY OFF
	ALTER DATABASE [TEST] SET ALLOW_SNAPSHOT_ISOLATION OFF
	ALTER DATABASE [TEST] SET PARAMETERIZATION SIMPLE
	ALTER DATABASE [TEST] SET READ_COMMITTED_SNAPSHOT OFF
	ALTER DATABASE [TEST] SET HONOR_BROKER_PRIORITY OFF
	ALTER DATABASE [TEST] SET  READ_WRITE
	ALTER DATABASE [TEST] SET RECOVERY SIMPLE
	ALTER DATABASE [TEST] SET  MULTI_USER
	ALTER DATABASE [TEST] SET PAGE_VERIFY CHECKSUM
	ALTER DATABASE [TEST] SET DB_CHAINING OFF
	
	print N'Created Database...' 
	INSERT INTO #UpdateVars Values (DB_ID(N'CreateTables'));
END

GO

USE [TEST]
GO

--Need to specify database owner before enabling change tracking
EXEC sp_changedbowner 'sa'
GO

/*Find out if we need to create the tables in our database*/
print N'Checking for table existence'

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'Location' AND type = 'U')
BEGIN 
	INSERT INTO #UpdateVars Values (N'CreateTables');
	print N'Tables are missing, creating them'
END
 
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	/****** Object:  FullTextCatalog [FullTextCatalog]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE FULLTEXT CATALOG [FullTextCatalog]WITH ACCENT_SENSITIVITY = ON
	AS DEFAULT
	AUTHORIZATION [dbo]
	
	
	/****** Object:  User [security]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE USER [security] FOR LOGIN [security] WITH DEFAULT_SCHEMA=[dbo]
	/****** Object:  User [Network Service]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE USER [Network Service] FOR LOGIN [NT AUTHORITY\NETWORK SERVICE] WITH DEFAULT_SCHEMA=[dbo]
	/****** Object:  User [Matlab]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE USER [Matlab] FOR LOGIN [Matlab] WITH DEFAULT_SCHEMA=[dbo]
	
END;
GO

DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	/****** Object:  Table [dbo].[LocationLink]    Script Date: 06/14/2011 13:13:51 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON

	
	CREATE TABLE [dbo].[LocationLink](
		[A] [bigint] NOT NULL,
		[B] [bigint] NOT NULL,
		[Username] [nchar](16) NOT NULL,
		[Created] [datetime] NOT NULL,
	 CONSTRAINT [PK_LocationLink] PRIMARY KEY CLUSTERED 
	(
		[A] ASC,
		[B] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	CREATE NONCLUSTERED INDEX [a] ON [dbo].[LocationLink] 
	(
		[A] ASC
	)
	INCLUDE ( [B]) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	CREATE NONCLUSTERED INDEX [b] ON [dbo].[LocationLink] 
	(
		[B] ASC
	)
	INCLUDE ( [A]) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The convention is that A is always less than B' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LocationLink', @level2type=N'COLUMN',@level2name=N'A'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Last username to modify the row' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LocationLink', @level2type=N'COLUMN',@level2name=N'Username'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Row Creation Date' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'LocationLink', @level2type=N'COLUMN',@level2name=N'Created'
END	

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectAllStructureLocationLinks]    Script Date: 06/14/2011 13:13:52 ******/
	
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	CREATE PROCEDURE [dbo].[SelectAllStructureLocationLinks]
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;

		-- Insert statements for procedure here
		Select * from LocationLink	 
	END
	')

GO

DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	/****** Object:  Table [dbo].[Location]    Script Date: 06/14/2011 13:13:52 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON
	SET ANSI_PADDING ON
	
	CREATE TABLE [dbo].[Location](
		[ID] [bigint] IDENTITY(1,1) NOT NULL,
		[ParentID] [bigint] NOT NULL,
		[X] [float] NOT NULL,
		[Y] [float] NOT NULL,
		[Z] [float] NOT NULL,
		[Verticies] [varbinary](max) NULL,
		[Closed] [bit] NOT NULL,
		[Version] [timestamp] NOT NULL,
		[Overlay] [varbinary](max) NULL,
		[Tags] [xml] NULL,
		[VolumeX] [float] NOT NULL,
		[VolumeY] [float] NOT NULL,
		[Terminal] [bit] NOT NULL,
		[OffEdge] [bit] NOT NULL,
		[Radius] [float] NOT NULL,
		[TypeCode] [smallint] NOT NULL,
		[LastModified] [datetime] NOT NULL,
		[Created] [datetime] NOT NULL,
		[Username] [nchar](16) NOT NULL,
	 CONSTRAINT [PK_Location] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
	
	/****** Object:  Table [dbo].[DeletedLocations]    Script Date: 06/14/2011 13:13:53 ******/
	CREATE TABLE [dbo].[DeletedLocations](
		[ID] [bigint] NOT NULL,
		[DeletedOn] [datetime] NOT NULL,
	 CONSTRAINT [PK_DeletedLocations] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	SET ANSI_PADDING OFF
	
	CREATE NONCLUSTERED INDEX [DeletedOn] ON [dbo].[DeletedLocations] 
	(
		[DeletedOn] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	
	CREATE NONCLUSTERED INDEX [LastModified] ON [dbo].[Location] 
	(
		[LastModified] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]

	CREATE NONCLUSTERED INDEX [ParentID] ON [dbo].[Location] 
	(
		[ParentID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	CREATE NONCLUSTERED INDEX [Z] ON [dbo].[Location] 
	(
		[Z] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Structure which we belong to' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'ParentID'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'A binary formatted series of X,Y doubles which can be specified to create polygons or lines' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Verticies'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Defines whether Vertices form a closed figure (The last vertex connects to the first)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Closed'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'An image centered on X,Y,Z which specifies which surrounding pixels are part of location' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Overlay'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'VolumeX is the location in volume space.  It exists so that data analysis code does not need to implement transforms' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'VolumeX'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'VolumeY is the location in volume space.  It exists so that data analysis code does not need to implement transforms' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'VolumeY'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Set to true if this location is the edge of a structure and cannot be extended.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Terminal'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'This bit is set if the structure leaves the volume at this location' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'OffEdge'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'0 = Point, 1 = Circle, 2 =' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'TypeCode'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the location was last modified' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'LastModified'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the location was created' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Created'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Last username to modify the row' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Username'
END

	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
	EXEC('
	/****** Object:  View [dbo].[UserActivity]    Script Date: 06/14/2011 13:13:53 ******/
	CREATE VIEW [dbo].[UserActivity]
	AS
	SELECT DISTINCT CAST(Username AS NVarchar(16)) AS Username, COUNT(ID) AS TotalModifications, MAX(LastModified) AS LastActiveDate
	FROM         dbo.Location
	GROUP BY Username
	');
	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
BEGIN
	EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
	Begin DesignProperties = 
	   Begin PaneConfigurations = 
		  Begin PaneConfiguration = 0
			 NumPanes = 4
			 Configuration = "(H (1[40] 4[20] 2[20] 3) )"
		  End
		  Begin PaneConfiguration = 1
			 NumPanes = 3
			 Configuration = "(H (1 [50] 4 [25] 3))"
		  End
		  Begin PaneConfiguration = 2
			 NumPanes = 3
			 Configuration = "(H (1 [50] 2 [25] 3))"
		  End
		  Begin PaneConfiguration = 3
			 NumPanes = 3
			 Configuration = "(H (4 [30] 2 [40] 3))"
		  End
		  Begin PaneConfiguration = 4
			 NumPanes = 2
			 Configuration = "(H (1 [56] 3))"
		  End
		  Begin PaneConfiguration = 5
			 NumPanes = 2
			 Configuration = "(H (2 [66] 3))"
		  End
		  Begin PaneConfiguration = 6
			 NumPanes = 2
			 Configuration = "(H (4 [50] 3))"
		  End
		  Begin PaneConfiguration = 7
			 NumPanes = 1
			 Configuration = "(V (3))"
		  End
		  Begin PaneConfiguration = 8
			 NumPanes = 3
			 Configuration = "(H (1[56] 4[18] 2) )"
		  End
		  Begin PaneConfiguration = 9
			 NumPanes = 2
			 Configuration = "(H (1 [75] 4))"
		  End
		  Begin PaneConfiguration = 10
			 NumPanes = 2
			 Configuration = "(H (1[66] 2) )"
		  End
		  Begin PaneConfiguration = 11
			 NumPanes = 2
			 Configuration = "(H (4 [60] 2))"
		  End
		  Begin PaneConfiguration = 12
			 NumPanes = 1
			 Configuration = "(H (1) )"
		  End
		  Begin PaneConfiguration = 13
			 NumPanes = 1
			 Configuration = "(V (4))"
		  End
		  Begin PaneConfiguration = 14
			 NumPanes = 1
			 Configuration = "(V (2))"
		  End
		  ActivePaneConfig = 0
	   End
	   Begin DiagramPane = 
		  Begin Origin = 
			 Top = 0
			 Left = 0
		  End
		  Begin Tables = 
			 Begin Table = "Location"
				Begin Extent = 
				   Top = 6
				   Left = 38
				   Bottom = 114
				   Right = 189
				End
				DisplayFlags = 280
				TopColumn = 0
			 End
		  End
	   End
	   Begin SQLPane = 
	   End
	   Begin DataPane = 
		  Begin ParameterDefaults = ""
		  End
		  Begin ColumnWidths = 9
			 Width = 284
			 Width = 1500
			 Width = 1500
			 Width = 2280
			 Width = 1500
			 Width = 1500
			 Width = 1500
			 Width = 1500
			 Width = 1500
		  End
	   End
	   Begin CriteriaPane = 
		  Begin ColumnWidths = 12
			 Column = 1440
			 Alias = 1185
			 Table = 1170
			 Output = 720
			 Append = 1400
			 NewValue = 1170
			 SortType = 1350
			 SortOrder = 1410
			 GroupBy = 1350
			 Filter = 1350
			 Or = 1350
			 Or = 1350
			 Or = 1350
		  End
	   End
	End
	' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'UserActivity'
	
	EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPaneCount', @value=1 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'UserActivity'

END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')

IF @db_id IS NOT NULL
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectSectionLocationsAndLinks]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinks]
		-- Add the parameters for the stored procedure here
		@Z float,
		@QueryDate datetime
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
		
		IF @QueryDate IS NOT NULL
			Select * from Location 
			where Z = @Z AND LastModified >= @QueryDate
		ELSE
			Select * from Location 
			where Z = @Z	
			
		IF @QueryDate IS NOT NULL
			-- Insert statements for procedure here
			Select * from LocationLink
			 WHERE (
						(A in 
							(SELECT ID
								FROM [dbo].[Location]
								WHERE Z = @Z
							)
						)
						OR
						(B in 
							(SELECT ID
								FROM [dbo].[Location]
								WHERE Z = @Z
							)
						)
					)
					AND Created >= @QueryDate
		ELSE
			-- Insert statements for procedure here
			Select * from LocationLink
			 WHERE ((A in 
			(SELECT ID
			  FROM [dbo].[Location]
			  WHERE Z = @Z)
			 )
			  OR
			  (B in 
			(SELECT ID
			  FROM [dbo].[Location]
			  WHERE Z = @Z)
			 ))
		
		 
	END
	')
	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectSectionLocationLinks]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	CREATE PROCEDURE [dbo].[SelectSectionLocationLinks]
		-- Add the parameters for the stored procedure here
		@Z float,
		@QueryDate datetime
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
			
		Select * from LocationLink
		 WHERE (((A in 
		(SELECT ID
		  FROM [dbo].[Location]
		  WHERE Z >= @Z)
		 )
		  AND
		  (B in 
		(SELECT ID
		  FROM [dbo].[Location]
		  WHERE Z <= @Z)
		 ))
		 OR
		 ((A in
		 (SELECT ID
		  FROM [dbo].[Location]
		  WHERE Z <= @Z)
		 )
		  AND
		  (B in 
		(SELECT ID
		  FROM [dbo].[Location]
		  WHERE Z >= @Z)
		 )))
		 AND Created >= @QueryDate
		 
	END
	')
	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
	EXEC('
	/****** Object:  StoredProcedure [dbo].[ApproximatestructureLocation]    Script Date: 06/14/2011 13:13:53 ******/
	CREATE PROCEDURE [dbo].[ApproximatestructureLocation]
	@StructureID int
	AS
		select SUM(VolumeX*Radius*Radius)/SUM(Radius*Radius) as X,SUM(VolumeY*Radius*Radius)/SUM(Radius*Radius) as Y,SUM(Z*Radius*Radius)/SUM(Radius*Radius) as Z, AVG(Radius) as Radius
		from dbo.Location where ParentID=@StructureID
	')

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	/****** Object:  Table [dbo].[StructureType]    Script Date: 06/14/2011 13:13:53 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON
	SET ANSI_PADDING ON
	
	CREATE TABLE [dbo].[StructureType](
		[ID] [bigint] IDENTITY(1,1) NOT NULL,
		[ParentID] [bigint] NULL,
		[Name] [nchar](128) NOT NULL,
		[Notes] [ntext] NULL,
		[MarkupType] [nchar](16) NOT NULL,
		[Tags] [xml] NULL,
		[StructureTags] [xml] NULL,
		[Abstract] [bit] NOT NULL,
		[Color] [int] NOT NULL,
		[Version] [timestamp] NOT NULL,
		[Code] [nchar](16) NOT NULL,
		[HotKey] [char](1) NOT NULL,
		[Username] [nchar](16) NOT NULL,
		[LastModified] [datetime] NOT NULL,
		[Created] [datetime] NOT NULL,
	 CONSTRAINT [PK_StructureType] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
	
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	SET ANSI_PADDING OFF
	CREATE NONCLUSTERED INDEX [ParentID] ON [dbo].[StructureType] 
	(
		[ParentID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Point,Line,Poly' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureType', @level2type=N'COLUMN',@level2name=N'MarkupType'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Strings seperated by semicolins' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureType', @level2type=N'COLUMN',@level2name=N'Tags'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Code used to identify these items in the UI' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureType', @level2type=N'COLUMN',@level2name=N'Code'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Hotkey used to create a structure of this type' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureType', @level2type=N'COLUMN',@level2name=N'HotKey'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Last username to modify the row' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureType', @level2type=N'COLUMN',@level2name=N'Username'
	
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	/****** Object:  Table [dbo].[Structure]    Script Date: 06/14/2011 13:13:53 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON
	SET ANSI_PADDING ON
	CREATE TABLE [dbo].[Structure](
		[ID] [bigint] IDENTITY(1,1) NOT NULL,
		[TypeID] [bigint] NOT NULL,
		[Notes] [ntext] NULL,
		[Verified] [bit] NOT NULL,
		[Tags] [xml] NULL,
		[Confidence] [float] NOT NULL,
		[Version] [timestamp] NOT NULL,
		[ParentID] [bigint] NULL,
		[Created] [datetime] NOT NULL,
		[Label] [varchar](64) NULL,
		[Username] [nchar](16) NOT NULL,
		[LastModified] [datetime] NOT NULL,
	 CONSTRAINT [PK_StructureBase] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
	
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	SET ANSI_PADDING OFF
	
	CREATE NONCLUSTERED INDEX [LastModified] ON [dbo].[Structure] 
	(
		[LastModified] DESC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	CREATE NONCLUSTERED INDEX [ParentID] ON [dbo].[Structure] 
	(
		[ParentID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	CREATE NONCLUSTERED INDEX [TypeID] ON [dbo].[Structure] 
	(
		[TypeID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Strings seperated by semicolins' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Tags'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'How certain is it that the structure is what we say it is' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Confidence'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Records last write time' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Version'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'If the structure is contained in a larger structure (Synapse for a cell) this index contains the index of the parent' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'ParentID'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Date the structure was created' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Created'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Additional Label for structure in UI' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Label'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Last username to modify the row' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Structure', @level2type=N'COLUMN',@level2name=N'Username'
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectStructureLocations]    Script Date: 06/14/2011 13:13:53 ******/
	CREATE PROCEDURE [dbo].[SelectStructureLocations]
		-- Add the parameters for the stored procedure here
		@StructureID bigint
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;

		-- Insert statements for procedure here
		SELECT L.ID
		   ,[ParentID]
		  ,[VolumeX]
		  ,[VolumeY]
		  ,[Z]
		  ,[Radius]
		  ,J.TypeID
		  ,[X]
		  ,[Y]
		  FROM [dbo].[Location] L
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure
			WHERE ID = @StructureID OR ParentID = @StructureID) J
		  ON L.ParentID = J.ID
		  ORDER BY ID
	END
	');
	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectStructureLocationLinks]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	CREATE PROCEDURE [dbo].[SelectStructureLocationLinks]
		-- Add the parameters for the stored procedure here
		@StructureID bigint
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;

		-- Insert statements for procedure here
		Select * from LocationLink
		 WHERE (A in 
		(SELECT L.ID
		  FROM [dbo].[Location] L
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure
			WHERE ID = @StructureID OR ParentID = @StructureID ) J
		  ON L.ParentID = J.ID))
		  OR
		  (B in 
		(SELECT L.ID
		  FROM [dbo].[Location] L
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure
			WHERE ID = @StructureID OR ParentID = @StructureID ) J
		  ON L.ParentID = J.ID))
		 
	END
	')
	
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL	
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectAllStructureLocations]    Script Date: 06/14/2011 13:13:53 ******/
	CREATE PROCEDURE [dbo].[SelectAllStructureLocations]
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;

		-- Insert statements for procedure here
		SELECT L.ID
		   ,[ParentID]
		  ,[VolumeX]
		  ,[VolumeY]
		  ,[Z]
		  ,[Radius]
		  ,J.TypeID
		  ,[X]
		  ,[Y]
		  FROM [dbo].[Location] L
		  INNER JOIN 
		   (SELECT ID, TYPEID
			FROM Structure) J
		  ON L.ParentID = J.ID
		  ORDER BY ID
	END
	'); 
GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	/****** Object:  Table [dbo].[StructureTemplates]    Script Date: 06/14/2011 13:13:53 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON
	SET ANSI_PADDING ON
	
	
	CREATE TABLE [dbo].[StructureTemplates](
		[ID] [bigint] IDENTITY(1,1) NOT NULL,
		[Name] [char](64) NOT NULL,
		[StructureTypeID] [bigint] NOT NULL,
		[StructureTags] [text] NOT NULL,
		[Version] [timestamp] NOT NULL,
	 CONSTRAINT [PK_StructureTemplates] PRIMARY KEY CLUSTERED 
	(
		[ID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
	
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	SET ANSI_PADDING OFF
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Name of template' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureTemplates', @level2type=N'COLUMN',@level2name=N'Name'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The structure type which is created when using the template' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureTemplates', @level2type=N'COLUMN',@level2name=N'StructureTypeID'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The tags to create with the new structure type' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureTemplates', @level2type=N'COLUMN',@level2name=N'StructureTags'
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN
	/****** Object:  Table [dbo].[StructureLink]    Script Date: 06/14/2011 13:13:53 ******/
	SET ANSI_NULLS ON
	SET QUOTED_IDENTIFIER ON
	
	CREATE TABLE [dbo].[StructureLink](
		[SourceID] [bigint] NOT NULL,
		[TargetID] [bigint] NOT NULL,
		[Bidirectional] [bit] NOT NULL,
		[Tags] [xml] NULL,
		[Username] [nchar](16) NOT NULL,
		[Created] [datetime] NOT NULL,
		[LastModified] [datetime] NOT NULL
	) ON [PRIMARY]
	
END

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	CREATE NONCLUSTERED INDEX [SourceID] ON [dbo].[StructureLink] 
	(
		[SourceID] ASC
	)
	INCLUDE ( [TargetID]) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	CREATE NONCLUSTERED INDEX [TargetID] ON [dbo].[StructureLink] 
	(
		[TargetID] ASC
	)
	INCLUDE ( [SourceID]) WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Last username to modify the row' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureLink', @level2type=N'COLUMN',@level2name=N'Username'
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Row Creation Date' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'StructureLink', @level2type=N'COLUMN',@level2name=N'Created'
	
END


GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
	EXEC('
	/****** Object:  StoredProcedure [dbo].[SelectStructuresForSection]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	CREATE PROCEDURE [dbo].[SelectStructuresForSection]
		-- Add the parameters for the stored procedure here
		@Z float,
		@QueryDate datetime
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
		
		declare @StructsOnSection TABLE 
		(
			ID bigint
		)
		INSERT INTO @StructsOnSection (ID)
		Select distinct(ParentID) from Location where Z = @Z
			
		if @QueryDate IS NULL
			Select * from Structure
			where ID in (Select ID from @StructsOnSection)	
		else
			Select * from Structure
			where ID in (Select ID from @StructsOnSection)
				AND LastModified >= @QueryDate
			
		

		Select * from StructureLink L
		where (L.TargetID in (Select ID from @StructsOnSection))
		   OR (L.SourceID in (Select ID from @StructsOnSection)) 
	END
	');
	

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
	EXEC('	
	/****** Object:  StoredProcedure [dbo].[MergeStructures]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	Create PROCEDURE [dbo].[MergeStructures]
		-- Add the parameters for the stored procedure here
		@KeepStructureID bigint,
		@MergeStructureID bigint
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
		
		update Location 
		set ParentID = @KeepStructureID 
		where ParentID = @MergeStructureID

		update Structure
		set ParentID = @KeepStructureID 
		where ParentID = @MergeStructureID
		
		update StructureLink
		set TargetID = @KeepStructureID
		where TargetID = @MergeStructureID
		
		update StructureLink
		set SourceID = @KeepStructureID
		where SourceID = @MergeStructureID

		Delete Structure
		where ID = @MergeStructureID
		
	END
	');
	

GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
	EXEC('
	
	/****** Object:  StoredProcedure [dbo].[SelectStructuresAndLinks]    Script Date: 06/14/2011 13:13:53 ******/
	-- =============================================
	-- Author:		<Author,,Name>
	-- Create date: <Create Date,,>
	-- Description:	<Description,,>
	-- =============================================
	Create PROCEDURE [dbo].[SelectStructuresAndLinks]
		-- Add the parameters for the stored procedure here
	AS
	BEGIN
		-- SET NOCOUNT ON added to prevent extra result sets from
		-- interfering with SELECT statements.
		SET NOCOUNT ON;
		
		Select * from Structure
		
		-- Insert statements for procedure here
		Select * from StructureLink
		
	END
	'); 


GO
DECLARE @db_id VARCHAR(100);
SET @db_id = (select * from #UpdateVars where Version='CreateTables')
IF @db_id IS NOT NULL
BEGIN	
	/****** Object:  Default [DF_LocationLink_Username]    Script Date: 06/14/2011 13:13:51 ******/
	ALTER TABLE [dbo].[LocationLink] ADD  CONSTRAINT [DF_LocationLink_Username]  DEFAULT (N'') FOR [Username]
	/****** Object:  Default [DF_LocationLink_Created]    Script Date: 06/14/2011 13:13:51 ******/
	ALTER TABLE [dbo].[LocationLink] ADD  CONSTRAINT [DF_LocationLink_Created]  DEFAULT (getutcdate()) FOR [Created]
	/****** Object:  Default [DF_Location_Closed]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_Closed]  DEFAULT ((0)) FOR [Closed]
	/****** Object:  Default [DF_Location_Flagged]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_Flagged]  DEFAULT ((0)) FOR [Terminal]
	/****** Object:  Default [DF_Location_OffEdge]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_OffEdge]  DEFAULT ((0)) FOR [OffEdge]
	/****** Object:  Default [DF_Location_Radius]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_Radius]  DEFAULT ((128)) FOR [Radius]
	/****** Object:  Default [DF_Location_TypeCode]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_TypeCode]  DEFAULT ((1)) FOR [TypeCode]
	/****** Object:  Default [DF_Location_LastModified]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
	/****** Object:  Default [DF_Location_Created]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_Created]  DEFAULT (getutcdate()) FOR [Created]
	/****** Object:  Default [DF_Location_Username]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location] ADD  CONSTRAINT [DF_Location_Username]  DEFAULT (N'') FOR [Username]
	/****** Object:  Default [DF_DeletedLocations_DeletedOn]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[DeletedLocations] ADD  CONSTRAINT [DF_DeletedLocations_DeletedOn]  DEFAULT (getutcdate()) FOR [DeletedOn]
	/****** Object:  Default [DF_StructureType_MarkupType]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_MarkupType]  DEFAULT (N'Point') FOR [MarkupType]
	/****** Object:  Default [DF_StructureType_Abstract]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_Abstract]  DEFAULT ((0)) FOR [Abstract]
	/****** Object:  Default [DF_StructureType_Color]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_Color]  DEFAULT (0xFFFFFF) FOR [Color]
	/****** Object:  Default [DF_StructureType_Code]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_Code]  DEFAULT (N'No Code') FOR [Code]
	/****** Object:  Default [DF_StructureType_HotKey]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_HotKey]  DEFAULT ('\0') FOR [HotKey]
	/****** Object:  Default [DF_StructureType_Username]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_Username]  DEFAULT (N'') FOR [Username]
	/****** Object:  Default [DF_StructureType_LastModified]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
	/****** Object:  Default [DF_StructureType_Created]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType] ADD  CONSTRAINT [DF_StructureType_Created]  DEFAULT (getutcdate()) FOR [Created]
	/****** Object:  Default [DF_StructureBase_Verified]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure] ADD  CONSTRAINT [DF_StructureBase_Verified]  DEFAULT ((0)) FOR [Verified]
	/****** Object:  Default [DF_StructureBase_Confidence]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure] ADD  CONSTRAINT [DF_StructureBase_Confidence]  DEFAULT ((0.5)) FOR [Confidence]
	/****** Object:  Default [DF_Structure_Created]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure] ADD  CONSTRAINT [DF_Structure_Created]  DEFAULT (getutcdate()) FOR [Created]
	/****** Object:  Default [DF_Structure_Username]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure] ADD  CONSTRAINT [DF_Structure_Username]  DEFAULT (N'') FOR [Username]
	/****** Object:  Default [DF_Structure_LastModified]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure] ADD  CONSTRAINT [DF_Structure_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
	/****** Object:  Default [DF_StructureTemplates_Tags]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureTemplates] ADD  CONSTRAINT [DF_StructureTemplates_Tags]  DEFAULT ('') FOR [StructureTags]
	/****** Object:  Default [DF_StructureLink_Bidirectional]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Bidirectional]  DEFAULT ((0)) FOR [Bidirectional]
	/****** Object:  Default [DF_StructureLink_Username]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Username]  DEFAULT (N'') FOR [Username]
	/****** Object:  Default [DF_StructureLink_Created]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink] ADD  CONSTRAINT [DF_StructureLink_Created]  DEFAULT (getutcdate()) FOR [Created]
	/****** Object:  Default [DF_StructureLink_LastModified]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink] ADD  CONSTRAINT [DF_StructureLink_LastModified]  DEFAULT (getutcdate()) FOR [LastModified]
	/****** Object:  ForeignKey [FK_LocationLink_Location]    Script Date: 06/14/2011 13:13:51 ******/
	ALTER TABLE [dbo].[LocationLink]  WITH CHECK ADD  CONSTRAINT [FK_LocationLink_Location] FOREIGN KEY([A])
	REFERENCES [dbo].[Location] ([ID])
	
	ALTER TABLE [dbo].[LocationLink] CHECK CONSTRAINT [FK_LocationLink_Location]
	
	/****** Object:  ForeignKey [FK_LocationLink_Location1]    Script Date: 06/14/2011 13:13:51 ******/
	ALTER TABLE [dbo].[LocationLink]  WITH CHECK ADD  CONSTRAINT [FK_LocationLink_Location1] FOREIGN KEY([B])
	REFERENCES [dbo].[Location] ([ID])
	
	ALTER TABLE [dbo].[LocationLink] CHECK CONSTRAINT [FK_LocationLink_Location1]
	
	/****** Object:  ForeignKey [FK_Location_StructureBase1]    Script Date: 06/14/2011 13:13:52 ******/
	ALTER TABLE [dbo].[Location]  WITH CHECK ADD  CONSTRAINT [FK_Location_StructureBase1] FOREIGN KEY([ParentID])
	REFERENCES [dbo].[Structure] ([ID])
	ON DELETE CASCADE
	
	ALTER TABLE [dbo].[Location] CHECK CONSTRAINT [FK_Location_StructureBase1]
	
	/****** Object:  ForeignKey [FK_StructureType_StructureType]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureType]  WITH CHECK ADD  CONSTRAINT [FK_StructureType_StructureType] FOREIGN KEY([ParentID])
	REFERENCES [dbo].[StructureType] ([ID])
	
	ALTER TABLE [dbo].[StructureType] CHECK CONSTRAINT [FK_StructureType_StructureType]
	
	/****** Object:  ForeignKey [FK_Structure_Structure]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure]  WITH CHECK ADD  CONSTRAINT [FK_Structure_Structure] FOREIGN KEY([ParentID])
	REFERENCES [dbo].[Structure] ([ID])
	
	ALTER TABLE [dbo].[Structure] CHECK CONSTRAINT [FK_Structure_Structure]
	
	/****** Object:  ForeignKey [FK_StructureBase_StructureType]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[Structure]  WITH CHECK ADD  CONSTRAINT [FK_StructureBase_StructureType] FOREIGN KEY([TypeID])
	REFERENCES [dbo].[StructureType] ([ID])
	
	ALTER TABLE [dbo].[Structure] CHECK CONSTRAINT [FK_StructureBase_StructureType]
	
	/****** Object:  ForeignKey [FK_StructureTemplates_StructureType]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureTemplates]  WITH CHECK ADD  CONSTRAINT [FK_StructureTemplates_StructureType] FOREIGN KEY([StructureTypeID])
	REFERENCES [dbo].[StructureType] ([ID])
	
	ALTER TABLE [dbo].[StructureTemplates] CHECK CONSTRAINT [FK_StructureTemplates_StructureType]
	
	/****** Object:  ForeignKey [FK_StructureLinkSource_StructureBaseID]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink]  WITH CHECK ADD  CONSTRAINT [FK_StructureLinkSource_StructureBaseID] FOREIGN KEY([SourceID])
	REFERENCES [dbo].[Structure] ([ID])
	
	ALTER TABLE [dbo].[StructureLink] CHECK CONSTRAINT [FK_StructureLinkSource_StructureBaseID]
	
	/****** Object:  ForeignKey [FK_StructureLinkTarget_StructureBaseID]    Script Date: 06/14/2011 13:13:53 ******/
	ALTER TABLE [dbo].[StructureLink]  WITH CHECK ADD  CONSTRAINT [FK_StructureLinkTarget_StructureBaseID] FOREIGN KEY([TargetID])
	REFERENCES [dbo].[Structure] ([ID])
	
	ALTER TABLE [dbo].[StructureLink] CHECK CONSTRAINT [FK_StructureLinkTarget_StructureBaseID]
END
	
/* Create a version table that tracks what schema version the database is using.  Create it if it doesn't exist. 
// Modelled after http://www.codeproject.com/KB/database/DatabaseSchemaVersioning.aspx
*/  
GO

Use [TEST]
GO

BEGIN TRANSACTION main
		
	--make sure the DBVersion table exists
	if(not exists (select (1) from dbo.sysobjects 
		 where id = object_id(N'[dbo].[DBVersion]') and 
		 OBJECTPROPERTY(id, N'IsUserTable') = 1))
	BEGIN
		 print N'Building the foundation ''DBVersion'' Table'
		 BEGIN TRANSACTION one
			 --build the table
			 CREATE TABLE DBVersion (
			  [DBVersionID] int NOT NULL ,
			  [Description] varchar (255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL ,
			  [ExecutionDate] datetime NOT NULL ,
			  [UserID] int NOT NULL
			 )

		 --any potential errors get reported,
		 --and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end

		 --insert the first version marker
		 INSERT INTO DBVersion values (1,'Build the DBVersion Table',
									   getDate(),User_ID())
		 
		COMMIT TRANSACTION one
	END
   --finished step one
   
   ---------------------------------------------------------------
   ---------------------------------------------------------------
   --Continuing, adding to this script is relatively simple...
   if(not(exists(select (1) from DBVersion where DBVersionID = 2)))
   begin
     print N'Modifying stored procedures for new VikingPlot version'
     BEGIN TRANSACTION two
		
		IF EXISTS(select * from sys.procedures where name = 'SelectAllStructureLocations')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectAllStructureLocations]');
		END
	
		 EXEC('
		 --add the column, or whatever else you may need to do
		 CREATE PROCEDURE [dbo].[SelectAllStructureLocations]
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				SELECT L.ID
				   ,[ParentID]
				  ,[VolumeX]
				  ,[VolumeY]
				  ,[Z]
				  ,[Radius]
				  ,[X]
				  ,[Y]
				  FROM [dbo].[Location] L
				  ORDER BY ID
			END
		');
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		IF EXISTS(select * from sys.procedures where name = 'SelectStructureLocations')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectStructureLocations]');
		END
     
		EXEC('
		CREATE PROCEDURE [dbo].[SelectStructureLocations]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			-- Insert statements for procedure here
			SELECT L.ID
			   ,[ParentID]
			  ,[VolumeX]
			  ,[VolumeY]
			  ,[Z]
			  ,[Radius]
			  ,[X]
			  ,[Y]
			  FROM [dbo].[Location] L
			  INNER JOIN 
			   (SELECT ID, TYPEID
				FROM Structure
				WHERE ID = @StructureID OR ParentID = @StructureID) J
			  ON L.ParentID = J.ID
			  ORDER BY ID
		END
		');

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		IF EXISTS(select * from sys.procedures where name = 'SelectAllStructures')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectAllStructures]');
		END
	    
		EXEC(' 
		CREATE PROCEDURE [dbo].[SelectAllStructures]
		-- Add the parameters for the stored procedure here
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			-- Insert statements for procedure here
			SELECT [ID]
			   ,[ParentID]
			   ,[TypeID]
			   ,[Label]
			   ,[LastModified]
			  FROM [dbo].Structure S
			  ORDER BY ID
		END
		');

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		IF EXISTS(select * from sys.procedures where name = 'SelectStructure')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectStructure]');
		END
	    
		EXEC('
		CREATE PROCEDURE [dbo].[SelectStructure]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			-- Insert statements for procedure here
			SELECT [ID]
			   ,[ParentID]
			   ,[TypeID]
			   ,[Label]
			   ,[LastModified]
			  FROM [dbo].Structure S
			  WHERE ID = @StructureID OR ParentID = @StructureID
			  ORDER BY ID
		END
		');
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		 --insert the second version marker
		 INSERT INTO DBVersion values (2, 
		   'Modified stored procedures for new VikingPlot version',getDate(),User_ID())

   COMMIT TRANSACTION two
end

---------------------------------------------------------------
---------------------------------------------------------------
--Continuing, adding to this script is relatively simple...
   if(not(exists(select (1) from DBVersion where DBVersionID = 3)))
   begin
     print N'Adding SP to calculate average locations for all structures'
     BEGIN TRANSACTION three
		
	    IF EXISTS(select * from sys.procedures where name = 'ApproximateStructureLocations')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[ApproximateStructureLocations]');
		END
		
		 EXEC('
		 CREATE PROCEDURE [dbo].[ApproximateStructureLocations]
         AS
         BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;
				
			 select ParentID, 
				SUM(VolumeX*Radius*Radius)/SUM(Radius*Radius) as X,
				SUM(VolumeY*Radius*Radius)/SUM(Radius*Radius) as Y,
				SUM(Z*Radius*Radius)/SUM(Radius*Radius) as Z,
				AVG(Radius) as Radius
			 from dbo.Location 
			 group by ParentID
		 END');

		 
		 --any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		  --insert the second version marker
		 INSERT INTO DBVersion values (3, 
		   'SP to calculate average locations for all structures',getDate(),User_ID())

	COMMIT TRANSACTION three
end

---------------------------------------------------------------
---------------------------------------------------------------
--Continuing, adding to this script is relatively simple...
   if(not(exists(select (1) from DBVersion where DBVersionID = 4)))
   begin
     print N'Adding SP to calculate average locations for one structure'
     BEGIN TRANSACTION four
     
		 IF EXISTS(select * from sys.procedures where name = 'ApproximateStructureLocation')
		 BEGIN
			EXEC('DROP PROCEDURE [dbo].[ApproximateStructureLocation]');
		 END
	
		 EXEC('
		 CREATE PROCEDURE [dbo].[ApproximateStructureLocation]
		 @StructureID int
         AS
         BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;
				
			select SUM(VolumeX*Radius*Radius)/SUM(Radius*Radius) as X,
				   SUM(VolumeY*Radius*Radius)/SUM(Radius*Radius) as Y,
				   SUM(Z*Radius*Radius)/SUM(Radius*Radius) as Z,
				   AVG(Radius) as Radius
			from dbo.Location 
			where ParentID=@StructureID

		 END');

		 
		 --any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		  --insert the second version marker
		 INSERT INTO DBVersion values (4, 
		   'SP to calculate average locations for all structures',getDate(),User_ID())

	COMMIT TRANSACTION four
	end
	
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 5)))
	begin
     print N'Adding contraints for links'
     BEGIN TRANSACTION five
	
		ALTER TABLE [dbo].[StructureLink]
		ADD CONSTRAINT chk_StructureLink_Self CHECK (SourceID != TargetID)
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end

		ALTER TABLE [dbo].[LocationLink]
		ADD CONSTRAINT chk_LocationLink_Self CHECK (A != B)
	
		 --any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end
		 
		  --insert the second version marker
		 INSERT INTO DBVersion values (5, 
		   'Constraints on location and structure links to the same object',getDate(),User_ID())

	COMMIT TRANSACTION five
	
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 6)))
	begin
     print N'Adding SP for latest modification by user'
     BEGIN TRANSACTION six
     
		IF EXISTS(select * from sys.procedures where name = 'SelectLastModifiedLocationByUsers')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectLastModifiedLocationByUsers]');
		END
	
		EXEC('
			CREATE PROCEDURE [dbo].[SelectLastModifiedLocationByUsers]
	
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			DECLARE @mindate datetime
			set @mindate = (select MIN(LastModified) from Location)

			select L.* from
				(
				select MAX(LastModifiedByUser.ID) as LatestID, LastModifiedByUser.Username
					from (
						select  Username, MAX(LastModified) as lm
							from Location
							where LastModified > @mindate /* Exclude dates with a default value */
								  Group BY Username
							)
							as L 
						inner join Location as LastModifiedByUser on LastModifiedByUser.Username = L.Username and L.lm = LastModifiedByUser.LastModified
					group by LastModifiedByUser.Username
					)
					as IDList
				inner join Location as L on IDList.LatestID = ID
			order by L.Username
		END');
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (6, 
		   'SP to calculate latest modification by user',getDate(),User_ID())

	COMMIT TRANSACTION six
	
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 7)))
	begin
     print N'Adding SP for number of connections per structure'
     BEGIN TRANSACTION seven
     
        IF EXISTS(select * from sys.procedures where name = 'SelectNumConnectionsPerStructure')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectNumConnectionsPerStructure]');
		END
	
		EXEC('
			 CREATE PROCEDURE [dbo].[SelectNumConnectionsPerStructure] 
			 AS 
			 (
				 select CS.ParentID as StructureID, ParentStructure.Label as Label, COUNT(CS.ParentID) as NumConnections 
				 from Structure CS
					INNER JOIN Structure ParentStructure
					ON CS.ParentID = ParentStructure.ID
				 WHERE 
				 (
						CS.ID in
						(
							Select SourceID from StructureLink
								WHERE 
								(SourceID in 
									(
									SELECT S.ID
										FROM [dbo].[Structure] S
									) 
								)
						)
				 )
				 OR 
				 (
						CS.ID in 
						(
							Select TargetID from StructureLink
								WHERE
								(TargetID in 
									(
										SELECT S.ID
										FROM [dbo].[Structure] S
									) 
								)
						)
			     )
				group by CS.ParentID, ParentStructure.Label

			)
			order by NumConnections DESC');
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (7, 
		   'SP to calculate num connections per structure',getDate(),User_ID())

	 COMMIT TRANSACTION seven
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 8)))
	begin
     print N'Adding SP for SelectStructureLocationsNoChildren'
     BEGIN TRANSACTION eight
		IF EXISTS(select * from sys.procedures where name = 'SelectStructureLocationLinksNoChildren')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectStructureLocationLinksNoChildren]');
		END
		
		IF EXISTS(select * from sys.procedures where name = 'SelectChildrenStructureLinks')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[SelectChildrenStructureLinks]');
		END
		
		EXEC('
			CREATE PROCEDURE [dbo].[SelectStructureLocationLinksNoChildren]
			-- Add the parameters for the stored procedure here
			@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				Select * from LocationLink
					 WHERE (A in 
						     (SELECT L.ID
							  FROM [dbo].[Location] L
								INNER JOIN 
								(SELECT ID, TYPEID
									FROM Structure
									WHERE ID = @StructureID) J
								ON L.ParentID = J.ID
								)
							)
							OR
							(B in 
								(SELECT L.ID
								 FROM [dbo].[Location] L
									INNER JOIN 
									(SELECT ID, TYPEID
										FROM Structure
										WHERE ID = @StructureID) J
									ON L.ParentID = J.ID
									)
							)
			END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   print N'SelectStructureLocationLinksNoChildren creation error'
		   ROLLBACK TRANSACTION
		   
		   RETURN
		 end
			
		EXEC('CREATE PROCEDURE [dbo].[SelectChildrenStructureLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				select * from StructureLink where 
				SourceID in (Select ID from Structure where ParentID = @StructureID) 
				or
				TargetID in (Select ID from Structure where ParentID = @StructureID)
			END');	
		
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   print N'SelectChildrenStructureLinks creation error'
		   ROLLBACK TRANSACTION
		   
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (8, 
		   'Adding SP for SelectStructureLocationsNoChildren',getDate(),User_ID())

	 COMMIT TRANSACTION eight
	end

    if(not(exists(select (1) from DBVersion where DBVersionID = 9)))
	begin
     print N'Adding SP for SelectStructureLabels'
     BEGIN TRANSACTION nine
	
		EXEC('
			 CREATE PROCEDURE [dbo].[SelectStructureLabels]
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select ID, Label from Structure where Label is NOT NULL
			 END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (9, 
		   'Adding SP for SelectStructureLabels',getDate(),User_ID())

	 COMMIT TRANSACTION nine
	end
	
	
    if(not(exists(select (1) from DBVersion where DBVersionID = 10)))
	begin
     print N'Adding SP for CountChildStructuresByType'
     BEGIN TRANSACTION ten
		
		EXEC('
			 CREATE PROCEDURE [dbo].[CountChildStructuresByType]
				@StructureID bigint
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select TypeID, COUNT(TypeID) as Count from Structure 
				where ParentID = @StructureID
				group by TypeID
			 END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (10, 
		   'Adding SP for CountChildStructuresByType',getDate(),User_ID())

	 COMMIT TRANSACTION ten
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 11)))
	begin
     print N'Adding SP for SelectRootStructures'
     BEGIN TRANSACTION eleven
		
		EXEC('
			 CREATE PROCEDURE [dbo].[SelectRootStructures]
			 AS
			 BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;
 
				select * from Structure 
				where ParentID IS NULL
			 END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (11, 
		   'Adding SP for SelectRootStructures',getDate(),User_ID())

	 COMMIT TRANSACTION eleven
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 12)))
	begin
     print N'Adding SP for SelectUnfinishedStructureBranches'
     BEGIN TRANSACTION twelve
		
		EXEC('
			 CREATE PROCEDURE SelectUnfinishedStructureBranches
				@StructureID bigint
			 AS
			 BEGIN
			 SET NOCOUNT ON;
  
			 select ID from 
				(select LocationID, COUNT(LocationID) as NumLinks from 
					(
						select A as LocationID from LocationLink 
							where
							(A in (Select L.ID from Location L where L.ParentID = @StructureID))
						union ALL
							select B as LocationID from LocationLink 
							where
							(B in (Select L.ID from Location L where L.ParentID = @StructureID))
					) as LinkedIDs
					Group BY LocationID ) as AllLocationLinks
					INNER JOIN
						(SELECT ID, Terminal, OffEdge from Location where Terminal = 0 and OffEdge = 0) L
					ON AllLocationLinks.LocationID = L.ID
					
					where AllLocationLinks.NumLinks <= 1
				order by ID 
			  END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (12, 
		   'Adding SP for SelectUnfinishedStructureBranches',getDate(),User_ID())

	 COMMIT TRANSACTION twelve
	end
	
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 13)))
	begin
     print N'Adding SP for SelectUnfinishedStructureBranchesWithPosition'
     BEGIN TRANSACTION thirteen
		
		EXEC('
			 CREATE PROCEDURE SelectUnfinishedStructureBranchesWithPosition
				@StructureID bigint
			 AS
			 BEGIN
			 SET NOCOUNT ON;
  
			 select ID, X, Y, Z, Radius from 
				(select LocationID, COUNT(LocationID) as NumLinks from 
					(
						select A as LocationID from LocationLink 
							where
							(A in (Select L.ID from Location L where L.ParentID = @StructureID))
						union ALL
							select B as LocationID from LocationLink 
							where
							(B in (Select L.ID from Location L where L.ParentID = @StructureID))
					) as LinkedIDs
					Group BY LocationID ) as AllLocationLinks
					INNER JOIN
						(SELECT ID, X,Y,Z, Radius from Location where Terminal = 0 and OffEdge = 0) L
					ON AllLocationLinks.LocationID = L.ID
					
					where AllLocationLinks.NumLinks <= 1
				order by ID
			  END');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (13, 
		   'Adding SP for SelectUnfinishedStructureBranchesWithPosition',getDate(),User_ID())

	 COMMIT TRANSACTION thirteen
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 14)))
	begin
     print N'Verify triggers are added'
     BEGIN TRANSACTION fourteen
		IF EXISTS(select * from sys.triggers where name = 'Location_update')
		BEGIN
			DROP TRIGGER [dbo].[Location_update];
		END
		
		IF EXISTS(select * from sys.triggers where name = 'Location_delete')
		BEGIN
			DROP TRIGGER [dbo].[Location_delete];
		END
		
		IF EXISTS(select * from sys.triggers where name = 'Structure_LastModified')
		BEGIN
			DROP TRIGGER [dbo].[Structure_LastModified];
		END
		
		IF EXISTS(select * from sys.triggers where name = 'StructureType_LastModified')
		BEGIN
			DROP TRIGGER [dbo].[StructureType_LastModified];
		END
		
		EXEC('
			 CREATE TRIGGER [dbo].[Location_update] 
				ON  [dbo].[Location]
				FOR UPDATE
				AS 
					Update dbo.Location
					Set LastModified = (GETUTCDATE())
					WHERE ID in (SELECT ID FROM inserted)
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;');
								
		EXEC('
			 CREATE TRIGGER [dbo].[Location_delete] 
			   ON  [dbo].[Location]
			   FOR DELETE
			 AS 
				INSERT INTO [dbo].[DeletedLocations] (ID)
				SELECT deleted.ID FROM deleted
				
				delete from LocationLink 
					where A in  (SELECT deleted.ID FROM deleted)
						or B in (SELECT deleted.ID FROM deleted)
				
				SET NOCOUNT ON;');
				
		EXEC('CREATE TRIGGER [dbo].[Structure_LastModified] 
			   ON  [dbo].[Structure]
			   FOR UPDATE
			AS 
				Update dbo.[Structure]
				Set LastModified = (GETUTCDATE())
				WHERE ID in (SELECT ID FROM inserted)
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;');
				
		EXEC('CREATE TRIGGER [dbo].[StructureType_LastModified] 
			   ON  [dbo].[StructureType]
			   FOR UPDATE
			AS 
				Update dbo.[StructureType]
				Set LastModified = (GETUTCDATE())
				WHERE ID in (SELECT ID FROM inserted)
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;');
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (14, 
		   'Verify triggers are added',getDate(),User_ID())

	 COMMIT TRANSACTION fourteen
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 15)))
	begin
     print N'Verify structure link Source->Target are unique'
     BEGIN TRANSACTION fifteen
		
		;WITH cte
			 AS (SELECT ROW_NUMBER() OVER (PARTITION BY SourceID, TargetID
											   ORDER BY ( SELECT 0)) RN
				 FROM StructureLink)
		delete from cte where RN > 1
		
		ALTER TABLE dbo.StructureLink
		    ADD CONSTRAINT source_target_unique UNIQUE(SOURCEID,TARGETID)
			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (15, 
		   'Verify structure link Source->Target are unique',getDate(),User_ID())

	 COMMIT TRANSACTION fifteen
	end
	
	if(not(exists(select (1) from DBVersion where DBVersionID = 16)))
	begin
     print N'Add procedure for power users to more safely change structures type without UPDATE SET WHERE query'
     BEGIN TRANSACTION sixteen
		  

		EXEC('
			 CREATE PROCEDURE UpdateStructureType
				@StructureID bigint,
				@TypeID bigint
			 AS
			 BEGIN
			 SET NOCOUNT ON;
  
			 UPDATE STRUCTURE SET TypeID=@TypeID WHERE ID = @STRUCTUREID
			 END');
				
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (16, 
		   'Add procedure for power users to more safely change structures type without UPDATE SET WHERE query',getDate(),User_ID())

	 COMMIT TRANSACTION sixteen
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 17)))
	begin
     print N'Create role for change logging'
     BEGIN TRANSACTION seventeen

	    CREATE ROLE [ChangeTracker]
		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (17, 
		   'Create role for change logging',getDate(),User_ID())

	 COMMIT TRANSACTION seventeen
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 18)))
	begin
     print N'Enable change logging'
	 BEGIN TRANSACTION eighteen
		
		

		EXEC sys.sp_cdc_enable_db
		
		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'Location',
			@capture_instance = N'Location',
			@role_name = N'ChangeTracker',
			@supports_net_changes = 1
		
		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'Structure',
			@capture_instance = N'Structure',
			@role_name = N'ChangeTracker',
			@supports_net_changes = 1

		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'StructureType',
			@capture_instance = N'StructureType',
			@role_name = N'ChangeTracker',
			@supports_net_changes = 1

		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'StructureLink',
			@capture_instance = N'StructureLink',
			@role_name = N'ChangeTracker'

		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'LocationLink',
			@capture_instance = N'LocationLink',
			@role_name = N'ChangeTracker'
				

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (18, 
		   'Enable change logging',getDate(),User_ID())

	 COMMIT TRANSACTION eighteen
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 19)))
	begin
     print N'Add stored procedures to query changes'
	 BEGIN TRANSACTION nineteen
		  
		EXEC('
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
				set @capture_instance_name = ''Structure''

				DECLARE @from_lsn binary(10), @to_lsn binary(10), @filter NVarChar(64)
				IF @begin_time IS NOT NULL
					set @from_lsn = sys.fn_cdc_map_time_to_lsn(''smallest greater than'', @begin_time)
				ELSE
					set @from_lsn  = sys.fn_cdc_get_min_lsn(@capture_instance_name)
	 
				IF @end_time IS NOT NULL
					set @to_lsn = sys.fn_cdc_map_time_to_lsn(''largest less than or equal'', @end_time)
				ELSE
					set @to_lsn  = sys.fn_cdc_get_max_lsn()
	 
				set @filter = N''all''

				if @structure_ID IS NOT NULL
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Structure(@from_lsn, @to_lsn, @filter) 
						where ID=@structure_ID 
						order by __$seqval
				ELSE 
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Structure(@from_lsn, @to_lsn, @filter) 
						order by __$seqval
			END')

		EXEC('
			CREATE PROCEDURE [dbo].SelectStructureLocationChangeLog
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
				set @capture_instance_name = ''Location''

				DECLARE @from_lsn binary(10), @to_lsn binary(10), @filter NVarChar(64)
				IF @begin_time IS NOT NULL
					set @from_lsn = sys.fn_cdc_map_time_to_lsn(''smallest greater than'', @begin_time)
				ELSE
					set @from_lsn  = sys.fn_cdc_get_min_lsn(@capture_instance_name)
	 
				IF @end_time IS NOT NULL
					set @to_lsn = sys.fn_cdc_map_time_to_lsn(''largest less than or equal'', @end_time)
				ELSE
					set @to_lsn  = sys.fn_cdc_get_max_lsn()
	 
				set @filter = N''all''

				if @structure_ID IS NOT NULL
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Location(@from_lsn, @to_lsn, @filter) 
						where ParentID=@structure_ID 
						order by __$seqval
				ELSE 
					SELECT *
						FROM cdc.fn_cdc_get_all_changes_Location(@from_lsn, @to_lsn, @filter) 
						order by __$seqval
			END
		')
		
				

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (19, 
		   'Add stored procedures to query changes',getDate(),User_ID())

	 COMMIT TRANSACTION nineteen
	end

--from here on, continually add steps in the previous manner as needed.
	COMMIT TRANSACTION main