/**** You need to replace the following templates to use this script ****/
/**** RABBIT = Name of the database  */
/**** {DATABASE_DIRECTORY} = Directory Datbase lives in if it needs to be created, with the trailing slash i.e. C:\Database\
*/
DECLARE @DATABASE_NAME VARCHAR(50)
SET @DATABASE_NAME = 'RABBIT'
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
INSERT INTO #UpdateVars Values (N'RABBIT');

DECLARE @db_id VARCHAR(100);
SET @db_id = db_id(@DATABASE_NAME)

print @db_id


IF @db_id IS NULL
BEGIN
	print N'Database does not exist, creating...' 
	
	declare @Path varchar(100)
	set @Path = N'C:\Database\RABBIT\'
	EXEC master.dbo.xp_create_subdir @Path
	
	/****** Object:  Database [RABBIT]    Script Date: 06/14/2011 13:13:50 ******/
	CREATE DATABASE [RABBIT] ON  PRIMARY 
		( NAME = N'RABBIT', FILENAME = N'C:\Database\RABBIT\RABBIT.mdf' , SIZE = 4096KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
		 LOG ON 
		( NAME = N'NEITZTEMPORALMONKEY_log', FILENAME = N'C:\Database\RABBIT\NEITZTEMPORALMONKEY_log.ldf' , SIZE = 4096KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
		
	ALTER DATABASE [RABBIT] SET COMPATIBILITY_LEVEL = 100
	
	IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
	begin
		EXEC [RABBIT].[dbo].[sp_fulltext_database] @action = 'enable'
	end
	
	ALTER DATABASE [RABBIT] SET ANSI_NULL_DEFAULT OFF
	ALTER DATABASE [RABBIT] SET ANSI_NULLS OFF
	ALTER DATABASE [RABBIT] SET ANSI_PADDING ON
	ALTER DATABASE [RABBIT] SET ANSI_WARNINGS OFF
	ALTER DATABASE [RABBIT] SET ARITHABORT OFF
	ALTER DATABASE [RABBIT] SET AUTO_CLOSE OFF
	ALTER DATABASE [RABBIT] SET AUTO_CREATE_STATISTICS ON
	ALTER DATABASE [RABBIT] SET AUTO_SHRINK OFF
	ALTER DATABASE [RABBIT] SET AUTO_UPDATE_STATISTICS ON
	ALTER DATABASE [RABBIT] SET CURSOR_CLOSE_ON_COMMIT OFF
	ALTER DATABASE [RABBIT] SET CURSOR_DEFAULT  GLOBAL
	ALTER DATABASE [RABBIT] SET CONCAT_NULL_YIELDS_NULL OFF
	ALTER DATABASE [RABBIT] SET NUMERIC_ROUNDABORT OFF
	ALTER DATABASE [RABBIT] SET QUOTED_IDENTIFIER OFF
	ALTER DATABASE [RABBIT] SET RECURSIVE_TRIGGERS OFF
	ALTER DATABASE [RABBIT] SET  DISABLE_BROKER
	ALTER DATABASE [RABBIT] SET AUTO_UPDATE_STATISTICS_ASYNC OFF
	ALTER DATABASE [RABBIT] SET DATE_CORRELATION_OPTIMIZATION OFF
	ALTER DATABASE [RABBIT] SET TRUSTWORTHY OFF
	ALTER DATABASE [RABBIT] SET ALLOW_SNAPSHOT_ISOLATION OFF
	ALTER DATABASE [RABBIT] SET PARAMETERIZATION SIMPLE
	ALTER DATABASE [RABBIT] SET READ_COMMITTED_SNAPSHOT OFF
	ALTER DATABASE [RABBIT] SET HONOR_BROKER_PRIORITY OFF
	ALTER DATABASE [RABBIT] SET  READ_WRITE
	ALTER DATABASE [RABBIT] SET RECOVERY SIMPLE
	ALTER DATABASE [RABBIT] SET  MULTI_USER
	ALTER DATABASE [RABBIT] SET PAGE_VERIFY CHECKSUM
	ALTER DATABASE [RABBIT] SET DB_CHAINING OFF
	
	print N'Created Database...' 
	INSERT INTO #UpdateVars Values (DB_ID(N'CreateTables'));
END

GO

USE [RABBIT]
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
	SET ANSI_PADDING ON
	
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
	EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'0 = Point, 1 = Circle, 2 =' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'RABBIT'
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
	SET ANSI_PADDING ON
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
	SET ANSI_PADDING ON
	
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
	SET ANSI_PADDING ON
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

Use [RABBIT]
GO
  
DECLARE @compat_level int
SET @compat_level = (SELECT compatibility_level FROM sys.databases WHERE name = 'RABBIT')
IF(@compat_level < 120)
BEGIN
	print N'Setting the database compatability level to SQL 2014'
	ALTER DATABASE [RABBIT] SET COMPATIBILITY_LEVEL = 120  
END
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
		
	    IF EXISTS(select * from sys.procedures where name = 'ApproximatestructureLocations')
		BEGIN
			EXEC('DROP PROCEDURE [dbo].[ApproximatestructureLocations]');
		END
		
		 EXEC('
		 CREATE PROCEDURE [dbo].[ApproximatestructureLocations]
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
     
		 IF EXISTS(select * from sys.procedures where name = 'ApproximatestructureLocation')
		 BEGIN
			EXEC('DROP PROCEDURE [dbo].[ApproximatestructureLocation]');
		 END
	
		 EXEC('
		 CREATE PROCEDURE [dbo].[ApproximatestructureLocation]
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
			 CREATE PROCEDURE UpdatestructureType
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

	if(not(exists(select (1) from DBVersion where DBVersionID = 20)))
	begin
     print N'Disable capture data cleanup job so change data is always retained'
	 BEGIN TRANSACTION twenty
		  
		EXECUTE sys.sp_cdc_drop_job 
			@job_type = N'cleanup'

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (20, 
		   'Disable capture data cleanup job so change data is always retained',getDate(),User_ID())

	 COMMIT TRANSACTION twenty
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 21)))
	begin
     print N'Replace deprecated text fields with nvarchar(max)'
	 BEGIN TRANSACTION twentyone
		
		ALTER TABLE Structure ALTER COLUMN Notes NVARCHAR(max) NULL
		ALTER TABLE StructureType ALTER COLUMN Notes NVARCHAR(max) NULL

		ALTER TABLE [dbo].[StructureTemplates] DROP CONSTRAINT [DF_StructureTemplates_Tags]
		ALTER TABLE StructureTemplates ALTER COLUMN StructureTags NVARCHAR(max) NOT NULL

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (21, 
		   'Replace deprecated text fields with nvarchar(max)',getDate(),User_ID())

	 COMMIT TRANSACTION twentyone
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 22)))
	begin
     print N'Copy notes on structure merge'
	 BEGIN TRANSACTION twentytwo
		
		EXEC('
			ALTER PROCEDURE [dbo].[MergeStructures]
			-- Add the parameters for the stored procedure here
			@KeepStructureID bigint,
			@MergeStructureID bigint
		AS
		BEGIN
			-- SET NOCOUNT ON added to prevent extra result sets from
			-- interfering with SELECT statements.
			SET NOCOUNT ON;

			declare @MergeNotes nvarchar(max)
			set @MergeNotes = (select notes from Structure where ID = @MergeStructureID)

			update Location 
			set ParentID = @KeepStructureID 
			where ParentID = @MergeStructureID

			update Structure
			set ParentID = @KeepStructureID 
			where ParentID = @MergeStructureID

			IF NOT (@MergeNotes IS NULL OR @MergeNotes = '''')
			BEGIN
				declare @crlf nvarchar(2)
				set @crlf = CHAR(13) + CHAR(10)

				declare @MergeHeader nvarchar(80)
				declare @MergeFooter nvarchar(80)
				set @MergeHeader = ''*****BEGIN MERGE FROM '' + CONVERT(nvarchar(80), @MergeStructureID) + ''*****''
				set @MergeFooter = ''*****END MERGE FROM '' + CONVERT(nvarchar(80), @MergeStructureID) + ''*****''

				update Structure
				set Notes = Notes + @crlf + @MergeHeader + @crlf + @MergeNotes + @crlf + @MergeFooter + @crlf
				where ID = @KeepStructureID
			END

			update StructureLink
			set TargetID = @KeepStructureID
			where TargetID = @MergeStructureID
		
			update StructureLink
			set SourceID = @KeepStructureID
			where SourceID = @MergeStructureID

			update Structure
			set Notes = ''Merged into structure '' + CONVERT(nvarchar(80), @KeepStructureID)
			where ID = @MergeStructureID

			delete Structure
			where ID = @MergeStructureID
		END
		')


		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (22, 
		   'Copy notes on structure merge',getDate(),User_ID())

	 COMMIT TRANSACTION twentytwo
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 23)))
	begin
     print N'Add procedure to select structures linked to specified structure via child structures'
	 BEGIN TRANSACTION twentythree
		
		EXEC('
			CREATE PROCEDURE [dbo].[SelectStructuresLinkedViaChildren]
			-- Add the parameters for the stored procedure here
			@StructureID bigint 
			AS
			BEGIN  

				IF OBJECT_ID(''tempdb..#ChildStructure'') IS NOT NULL DROP TABLE #ChildStructure
				IF OBJECT_ID(''tempdb..#LinkedStructures'') IS NOT NULL DROP TABLE #LinkedStructures 

				select ID into #ChildStructure from structure where ParentID = @StructureID
				select * into #LinkedStructures from StructureLink where SourceID in (Select ID from #ChildStructure) or TargetID in (Select ID from #CHildStructure)
				
				select Distinct ParentID from Structure where ID in (select SourceID from #LinkedStructures) or ID in (select TargetID from #LinkedStructures)

				DROP TABLE #ChildStructure
				DROP TABLE #LinkedStructures 
			END
			')
		
					
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (23, 
		   N'Add procedure to select structures linked to specified structure via child structures',getDate(),User_ID())

	 COMMIT TRANSACTION twentythree
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 24)))
	begin
     print N'Convert to spatial data types, create columns and populate'
	 BEGIN TRANSACTION twentyfour
		 
		ALTER TABLE Location ADD MosaicShape geometry
		ALTER TABLE Location ADD VolumeShape geometry


		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
      INSERT INTO DBVersion values (24, 
		    N'Convert to spatial data types, create columns and populate',getDate(),User_ID())

	  COMMIT TRANSACTION twentyfour
	 end
    go

	if(not(exists(select (1) from DBVersion where DBVersionID = 25)))
	begin
     print N'Convert to spatial data types, remove old columns'
	 BEGIN TRANSACTION twentyfive

		ALTER TABLE Location DROP COLUMN Verticies

		Update Location SET MosaicShape='CURVEPOLYGON(CIRCULARSTRING (' + STR(X-Radius,16,2) + ' ' + STR(Y,16,2) + ' ' + STR(Z) + ', '
																		+ STR(X,16,2) + ' '		   + STR(Y+Radius,16,2) + ' ' + STR(Z) + ', '
																		+ STR(X+Radius,16,2) + ' ' + STR(Y,16,2) + ' ' + STR(Z) + ', '
																		+ STR(X,16,2) + ' '		   + STR(Y-Radius,16,2) + ' ' + STR(Z) + ', '
																		+ STR(X-Radius,16,2) + ' ' + STR(Y,16,2) + ' ' + STR(Z) + ' ))' FROM Location WHERE Radius >= 0.1

		Update Location SET VolumeShape='CURVEPOLYGON(CIRCULARSTRING (' + STR(VolumeX-Radius,16,2) + ' ' + STR(VolumeY,16,2) + ' ' + STR(Z) + ', '
																		+ STR(VolumeX,16,2) + ' '		 + STR(VolumeY+Radius,16,2) + ' ' + STR(Z) + ', '
																		+ STR(VolumeX+Radius,16,2) + ' ' + STR(VolumeY,16,2) + ' ' + STR(Z) + ', '
																		+ STR(VolumeX,16,2) + ' '		 + STR(VolumeY-Radius,16,2) + ' ' + STR(Z) + ', '
																		+ STR(VolumeX-Radius,16,2) + ' ' + STR(VolumeY,16,2) + ' ' + STR(Z) + ' ))' FROM Location WHERE Radius >= 0.1

		Update Location SET MosaicShape='POINT (' + STR(X,16,2) + ' ' + STR(Y,16,2)  +' ' + STR(Z)  + ')' FROM Location WHERE Radius < 0.1

		Update Location SET VolumeShape='POINT (' + STR(VolumeX,16,2) + ' ' + STR(VolumeY,16,2)  +' ' + STR(Z)  + ')' FROM Location WHERE Radius < 0.1
		  
		ALTER TABLE Location ALTER COLUMN [MosaicShape] geometry NOT NULL
		ALTER TABLE Location ALTER COLUMN [VolumeShape] geometry NOT NULL
		   
		ALTER TABLE Location DROP COLUMN X 
		ALTER TABLE Location ADD X as ISNULL(MosaicShape.STCentroid().STX, ISNULL(MosaicShape.STX,0)) PERSISTED
		ALTER TABLE Location DROP COLUMN Y
		ALTER TABLE Location ADD Y as ISNULL(MosaicShape.STCentroid().STY, ISNULL(MosaicShape.STY,0)) PERSISTED

		ALTER TABLE Location DROP COLUMN VolumeX 
		ALTER TABLE Location ADD VolumeX as ISNULL(VolumeShape.STCentroid().STX, ISNULL(VolumeShape.STX,0)) PERSISTED
		ALTER TABLE Location DROP COLUMN VolumeY
		ALTER TABLE Location ADD VolumeY as ISNULL(VolumeShape.STCentroid().STY, ISNULL(VolumeShape.STY,0)) PERSISTED

		--ALTER TABLE Location DROP COLUMN Radius
		--ALTER TABLE Location ADD Radius as ISNULL(VolumeShape.STEnvelope().STX, ISNULL(VolumeShape.STX,0)) PERSISTED
		 			
		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (25, 
		    N'Convert to spatial data types, remove old columns',getDate(),User_ID())

	 COMMIT TRANSACTION twentyfive
	end 

	if(not(exists(select (1) from DBVersion where DBVersionID = 26)))
	begin
     print N'Add stored procedure to fetch locations for section' 
	 BEGIN TRANSACTION twentysix
	   EXEC('
			CREATE FUNCTION SectionLocations(@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location where Z = @Z
				);
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		EXEC('
			CREATE FUNCTION SectionLocationsModifiedAfterDate(@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location 
					where Z = @Z AND LastModified >= @QueryDate
				);
			')

		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			CREATE FUNCTION SectionLocationLinks(@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocations(@Z)) or B in (select ID from SectionLocations(@Z))
				);
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			CREATE FUNCTION SectionLocationLinksModifiedAfterDate(@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink where A in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate)) or B in (select ID from SectionLocationsModifiedAfterDate(@Z, @QueryDate))
				);
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	   INSERT INTO DBVersion values (26, 
		    N'Add stored procedure to fetch locations for section',getDate(),User_ID())
	 COMMIT TRANSACTION twentysix
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 27)))
	begin
     print N'Reset change data tracking for locations table to reflect schema change' 
	 BEGIN TRANSACTION twentyseven

	    EXEC sys.sp_cdc_enable_db
	    
		EXEC sys.sp_cdc_disable_table
			@source_schema = N'dbo',
			@source_name   = N'Location',
			@capture_instance = N'Location'

		EXEC sys.sp_cdc_enable_table
			@source_schema = N'dbo',
			@source_name = N'Location',
			@capture_instance = N'Location',
			@role_name = N'ChangeTracker',
			@supports_net_changes = 1

		EXECUTE sys.sp_cdc_drop_job 
			@job_type = N'cleanup'

	   INSERT INTO DBVersion values (27, 
		    N'Reset change data tracking for locations table to reflect schema change' ,getDate(),User_ID())
	 COMMIT TRANSACTION twentyseven
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 28)))
	begin
     print N'Create Spatial Indicies' 
	 BEGIN TRANSACTION twentyeight
	   
		CREATE SPATIAL INDEX [VolumeShape_Index] ON [dbo].[Location]
		(
			[VolumeShape]
		)USING  GEOMETRY_AUTO_GRID 
		WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
		CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
		

		CREATE SPATIAL INDEX [MosaicShape_Index] ON [dbo].[Location]
		(
			[MosaicShape]
		)USING  GEOMETRY_AUTO_GRID 
		WITH (BOUNDING_BOX =(0, 0, 150000, 150000), 
		CELLS_PER_OBJECT = 16, PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
		

	   INSERT INTO DBVersion values (28, 
		    N'Create Spatial Indicies' ,getDate(),User_ID())
	 COMMIT TRANSACTION twentyeight
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 29)))
	begin
     print N'Create Function for SelectStructureLinks for easy OData use' 
	 BEGIN TRANSACTION twentynine
	   
		EXEC('
			CREATE FUNCTION StructureLocationLinks(@StructureID bigint)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from LocationLink
						 WHERE (A in 
						(SELECT L.ID
						  FROM [Rabbit].[dbo].[Location] L
						  INNER JOIN 
						   (SELECT ID, TYPEID
							FROM Structure
							WHERE ID = @StructureID) J
						  ON L.ParentID = J.ID))
						  OR
						  (B in 
						(SELECT L.ID
						  FROM [Rabbit].[dbo].[Location] L
						  INNER JOIN 
						   (SELECT ID, TYPEID
							FROM Structure
							WHERE ID = @StructureID) J
						  ON L.ParentID = J.ID)))
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end


	   INSERT INTO DBVersion values (29, 
		    N'Create Function for SelectStructureLinks for easy OData use'  ,getDate(),User_ID())
	 COMMIT TRANSACTION twentynine
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 30)))
	begin
		
		print N'Add additional statistics'
		BEGIN TRANSACTION thirty

		  CREATE STATISTICS [_dta_stat_Location_ParentID_ID_Z] ON [dbo].[Location]([ParentID], [ID], [Z])
		  CREATE STATISTICS [_dta_stat_Location_Z_ID] ON [dbo].[Location]([Z], [ID])
		  CREATE STATISTICS [_dta_stat_Location_ID_ParentID] ON [dbo].[Location]([ID], [ParentID])
		  CREATE STATISTICS [_dta_stat_Structure_ParentID_ID] ON [dbo].[Structure]([ParentID], [ID])
		  CREATE STATISTICS [_dta_stat_Structure_ID_TypeID] ON [dbo].[Structure]([ID], [TypeID])

		  INSERT INTO DBVersion values (30, 
				N'Add additional statistics'  ,getDate(),User_ID())
		COMMIT TRANSACTION thirty
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 31)))
	begin
     print N'Create Functions for spatial queries' 
	 BEGIN TRANSACTION thirtyone
	    
		 EXEC('
			CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinksInBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds

				--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

				select * into #LocationsInBounds FROM Location where Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID
	 
				IF @QueryDate IS NOT NULL
					Select * from #LocationsInBounds where LastModified >= @QueryDate
				ELSE
					Select * from #LocationsInBounds
	 
				IF @QueryDate IS NOT NULL
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(select ID from #LocationsInBounds))
					  OR
					  (B in 
					(select ID from #LocationsInBounds)))
					 AND Created >= @QueryDate
				ELSE
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in (select ID from #LocationsInBounds))
							OR	
							(B in (select ID from #LocationsInBounds)))
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresInBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN 
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocationsInBounds'') IS NOT NULL DROP TABLE #SectionLocationsInBounds
					select * into #SectionLocationsInBounds from Location where (@bbox.STIntersects(VolumeShape) = 1) and Z = @Z AND Radius >= @MinRadius order by ParentID

					IF @QueryDate IS NOT NULL
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocationsInBounds) AND LastModified >= @QueryDate
					ELSE
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocationsInBounds)
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			CREATE PROCEDURE [dbo].[SelectSectionStructures]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN 
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
					select * into #SectionLocationsInBounds from Location where Z = @Z order by ParentID

					IF @QueryDate IS NOT NULL
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocations) AND LastModified >= @QueryDate
					ELSE
						select * from Structure where ID in (
							select distinct ParentID from #SectionLocations)
			END

			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN 
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
					IF OBJECT_ID(''tempdb..#SectionStructures'') IS NOT NULL DROP TABLE #SectionStructures
					select * into #SectionLocations from Location where Z = @Z order by ParentID
					select * into #SectionStructures from Structure where ID in (
							select distinct ParentID from #SectionLocations)

					IF @QueryDate IS NOT NULL
						select * from #SectionStructures where LastModified >= @QueryDate
					ELSE
						select * from #SectionStructures

					Select * from StructureLink L
					where (L.TargetID in (Select ID from #SectionStructures))
						OR (L.SourceID in (Select ID from #SectionStructures)) 
			END  
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 

		 EXEC('
			ALTER PROCEDURE [dbo].[SelectSectionLocationsAndLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
				select * into #SectionLocations from Location where Z = @Z ORDER BY ID
	
				IF @QueryDate IS NOT NULL
					Select * from #SectionLocations
					where LastModified >= @QueryDate
				ELSE
					Select * from #SectionLocations
		
				IF @QueryDate IS NOT NULL
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(SELECT ID
					  from #SectionLocations)
					 )
					  OR
					  (B in 
					(SELECT ID
					  from #SectionLocations)
					 ))
					 AND Created >= @QueryDate
				ELSE
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(SELECT ID
					  from #SectionLocations)
					 )
					  OR
					  (B in 
					(SELECT ID
					  from #SectionLocations)
					 )) 
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			ALTER PROCEDURE [dbo].[SelectSectionLocationLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsAboveZ'') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID(''tempdb..#LocationsBelowZ'') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z order by ID
				select * into #LocationsBelowZ from Location where Z <= @Z order by ID

	
				IF @QueryDate IS NOT NULL
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )))
					 AND Created >= @QueryDate
				ELSE
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 ))) 
	 
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			Create PROCEDURE [dbo].[SelectSectionLocationLinksInBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@bbox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				--This really needs to check if a line between the two location links intersects the bounding box.

				IF OBJECT_ID(''tempdb..#LocationsAboveZ'') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID(''tempdb..#LocationsBelowZ'') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z AND (@bbox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID 
				select * into #LocationsBelowZ from Location where Z <= @Z AND (@bbox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID

	
				IF @QueryDate IS NOT NULL
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )))
					 AND Created >= @QueryDate
				ELSE
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 ))) 
	 
			END

			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
		  
		  EXEC('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinksInBounds]
			-- Add the parameters for the stored procedure here
			@Z float,
			@BBox geometry,
			@MinRadius float,
			@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocationsInBounds'') IS NOT NULL DROP TABLE #SectionLocationsInBounds
					IF OBJECT_ID(''tempdb..#SectionStructuresInBounds'') IS NOT NULL DROP TABLE #SectionStructuresInBounds
					select * into #SectionLocationsInBounds from Location where (@bbox.STIntersects(VolumeShape) = 1) and Z = @Z AND Radius >= @MinRadius order by ParentID
					select * into #SectionStructuresInBounds from Structure where ID in (select distinct ParentID from #SectionLocationsInBounds)

					IF @QueryDate IS NOT NULL
						select * from #SectionStructuresInBounds where LastModified >= @QueryDate
					ELSE
						select * from #SectionStructuresInBounds
			  
					Select * from StructureLink L
					where (L.TargetID in (Select ID from #SectionStructuresInBounds))
						OR (L.SourceID in (Select ID from #SectionStructuresInBounds)) 
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
			DROP PROCEDURE [dbo].[SelectStructuresForSection]
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end 

	   INSERT INTO DBVersion values (31, 
		    N'Create Functions for spatial queries'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtyone
	end
	go

	if(not(exists(select (1) from DBVersion where DBVersionID = 32)))
	begin
     print N'Update spatial query functions'
	 BEGIN TRANSACTION thirtytwo
		 
		 EXEC('
			ALTER PROCEDURE [dbo].[SelectSectionLocationsAndLinksInBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@Radius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds

				--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

				select * into #LocationsInBounds FROM Location where Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @Radius order by ID
	 
				IF @QueryDate IS NOT NULL
					Select * from #LocationsInBounds where LastModified >= @QueryDate
				ELSE
					Select * from #LocationsInBounds
	 
				IF @QueryDate IS NOT NULL
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(select ID from #LocationsInBounds))
					  OR
					  (B in 
					(select ID from #LocationsInBounds)))
					 AND Created >= @QueryDate
				ELSE
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in (select ID from #LocationsInBounds))
							OR	
							(B in (select ID from #LocationsInBounds)))
	
	 
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (32, 
		    N'Update spatial query functions'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtytwo
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 33)))
	begin
     print N'Update spatial query functions'
	 BEGIN TRANSACTION thirtythree
		 
		 EXEC('
			ALTER PROCEDURE [dbo].[SelectSectionLocationsAndLinksInBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@Radius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds

				--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

				select * into #LocationsInBounds FROM Location where Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @Radius order by ID
	 
				IF @QueryDate IS NOT NULL
					Select * from #LocationsInBounds where LastModified >= @QueryDate
				ELSE
					Select * from #LocationsInBounds
	 
				IF @QueryDate IS NOT NULL
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in 
					(select ID from #LocationsInBounds))
					  OR
					  (B in 
					(select ID from #LocationsInBounds)))
					 AND Created >= @QueryDate
				ELSE
					-- Insert statements for procedure here
					Select * from LocationLink
					 WHERE ((A in (select ID from #LocationsInBounds))
							OR	
							(B in (select ID from #LocationsInBounds)))
	
	 
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (33, 
		    N'Update spatial query functions'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtythree
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 34)))
	begin
     print N'Add recommended statistics'
	 BEGIN TRANSACTION thirtyfour
		 
		 EXEC('
			CREATE NONCLUSTERED INDEX [Structure_ParentID_ID] ON [dbo].[Structure]
			(
				[ParentID] ASC,
				[ID] ASC
			)WITH (SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (34, 
		    N'Add recommended statistics'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtyfour
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 35)))
	begin
     print N'Add optimized Stored procedure for Structure locations and links'
	 BEGIN TRANSACTION thirtyfive
		 
		 EXEC('
			CREATE PROCEDURE [dbo].[SelectStructureLocationsAndLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				select * from Location where ParentID = @StructureID 

				IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
				select ID into #SectionLocations from Location where ParentID = @StructureID ORDER BY ID
	
				-- Insert statements for procedure here
				Select ll.* from LocationLink ll JOIN #SectionLocations sl ON (sl.ID = ll.A)
				UNION
				Select ll.* from LocationLink ll JOIN #SectionLocations sl ON (sl.ID = ll.B)
			END
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (35, 
		    N'Add optimized Stored procedure for Structure locations and links'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtyfive
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 36)))
	begin
     print N'Optimize stored procedure for SelectSectionStructuresAndLinks' 
	 BEGIN TRANSACTION thirtysix
	    
		 EXEC('
			ALTER PROCEDURE [dbo].[SelectSectionStructuresAndLinks]
				-- Add the parameters for the stored procedure here
				@Z float,
				@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
					select distinct ParentID into #SectionLocations from Location where Z = @Z order by ParentID

					IF @QueryDate IS NOT NULL
						select s.* from Structure s JOIN #SectionLocations l ON (l.ParentID = s.ID) where s.LastModified >= @QueryDate
					ELSE
						select s.* from Structure s JOIN #SectionLocations l ON (l.ParentID = s.ID)

					Select * from StructureLink L
					where (L.TargetID in (Select ParentID from #SectionLocations))
						OR (L.SourceID in (Select ParentID from #SectionLocations)) 
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (36, 
		     N'Optimize stored procedure for SelectSectionStructuresAndLinks'  ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtysix
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 37)))
	begin
     print N'Add check constraint to prevent reciprocal structure links' 
	 BEGIN TRANSACTION thirtyseven
		IF EXISTS(select * from sys.triggers where name = 'StructureLink_ReciprocalCheck')
		BEGIN
			DROP TRIGGER [dbo].[StructureLink_ReciprocalCheck];
		END
		 
		EXEC('CREATE TRIGGER [dbo].[StructureLink_ReciprocalCheck] 
		ON  [dbo].[StructureLink]
		AFTER INSERT, UPDATE
		AS 
			IF ((select count(SLA.SourceID)
				from inserted SLA 
				JOIN StructureLink SLB 
				ON (SLA.SourceID = SLB.TargetID AND SLA.TargetID = SLB.SourceID)) > 0)
				BEGIN
					RAISERROR(''Reciprocal structure links are not allowed'',14,1);
					ROLLBACK TRANSACTION;
					RETURN
				END
				')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (37, 
		     N'Add check constraint to prevent reciprocal structure links'   ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtyseven
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 38)))
	begin
     print N'Add User Defined Functions to help measure PSD and gap junction area' 
	 BEGIN TRANSACTION thirtyeight
		IF OBJECT_ID (N'dbo.ufnLineFromPoints', N'FN') IS NOT NULL
			DROP FUNCTION ufnLineFromPoints;
		IF OBJECT_ID (N'dbo.ufnLineFromAngleAndDistance', N'FN') IS NOT NULL
			DROP FUNCTION ufnLineFromAngleAndDistance;
		IF OBJECT_ID (N'dbo.ufnLineFromLinkedShapes', N'FN') IS NOT NULL
			DROP FUNCTION ufnLineFromLinkedShapes;
		
		Exec('
			CREATE FUNCTION dbo.ufnLineFromPoints(@P1 geometry, @P2 geometry)
			RETURNS geometry 
			AS 
			-- Returns a line where two circles intersect.  
			-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
			BEGIN
				DECLARE @ret geometry
				if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
					SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
															   STR(@P1.STY, 10,8) + '' '' +
															   STR(@P1.Z, 10,8) + '', '' +
															   STR(@P2.STX, 10,8) + '' '' +
															   STR(@P2.STY, 10,8) + '' '' +
															   STR(@P2.Z, 10,8) + '')'',0)
				ELSE
					SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
															   STR(@P1.STY, 10,8) + '', '' +
															   STR(@P2.STX, 10,8) + '' '' +
															   STR(@P2.STY, 10,8) + '')'',0)
				RETURN @ret
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		Exec('
			CREATE FUNCTION dbo.ufnLineFromAngleAndDistance(@angle float, @distance float, @offset geometry)
			RETURNS geometry
			AS 
			-- Returns a line centered on offset with @angle and total length = @distance
			BEGIN
				DECLARE @ret geometry
				DECLARE @P1X float
				DECLARE @P1Y float
				DECLARE @P2X float
				DECLARE @P2Y float
				DECLARE @Radius float
				DECLARE @Tau float
				set @Radius = @distance / 2.0 

				--Need to create a line centered on 0,0 so we can translate it to the center of S
				set @P1X = (COS(@Angle - PI()) * @Radius) + @offset.STX
				set @P1Y = (SIN(@Angle - PI()) * @Radius) + @offset.STY
				set @P2X = (COS(@Angle) * @Radius) + @offset.STX
				set @P2Y = (SIN(@Angle) * @Radius) + @offset.STY

				if @Offset.Z is NOT NULL
					set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
															STR(@P1Y, 10,8) + '' '' +
															STR(@offset.Z, 10, 8) + '', '' + 
															STR(@P2X, 10,8) + '' '' +
															STR(@P2Y, 10,8) + '' '' +
															STR(@offset.Z, 10, 8) + '')'',0)
				ELSE
					set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
															STR(@P1Y, 10,8) + '', '' + 
															STR(@P2X, 10,8) + '' '' +
															STR(@P2Y, 10,8) + '')'',0)
				  
				RETURN @ret
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			CREATE FUNCTION dbo.ufnLineFromLinkedShapes(@S geometry, @T geometry)
			RETURNS geometry 
			AS 
			-- Returns a line where two circles intersect.  
			-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
			BEGIN
				DECLARE @ret geometry
	
				IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
				BEGIN
					DECLARE @Points geometry
					SET @Points = @S.STBoundary().STIntersection(@T.STBoundary())
					SET @ret = DBO.ufnLineFromPoints(@Points.STGeometryN(1), @Points.STGeometryN(2))
				END
				ELSE
				BEGIN
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @Radius float
					DECLARE @Angle float
					set @SCenter = @S.STCentroid ( )
					set @TCenter = @T.STCentroid ( )
					set @Radius = SQRT(@T.STArea() / PI())
					set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX) + (PI() / 2.0)
		
					set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @TCenter)
				END
			 
				RETURN @ret
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (38, 
		      N'Add User Defined Functions to help measure PSD and gap junction area' ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtyeight
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 39)))
	begin
     print N'Add User Defined Functions to define constants such as XY scale' 
	 BEGIN TRANSACTION thirtynine
		IF OBJECT_ID (N'dbo.XYScale', N'FN') IS NOT NULL
			DROP FUNCTION XYScale;
		IF OBJECT_ID (N'dbo.ZScale', N'FN') IS NOT NULL
			DROP FUNCTION ZScale;
		IF OBJECT_ID (N'dbo.XYScaleUnits', N'FN') IS NOT NULL
		    DROP FUNCTION XYScaleUnits;
		IF OBJECT_ID (N'dbo.ZScaleUnits', N'FN') IS NOT NULL
			DROP FUNCTION ZScaleUnits;

		Exec('
			CREATE FUNCTION dbo.XYScale()
			RETURNS float 
			AS 
			-- Returns the scale in the XY axis
			BEGIN
				RETURN 2.176
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
		 		
		Exec('
			CREATE FUNCTION dbo.ZScale()
			RETURNS float 
			AS 
			-- Returns the scale in the Z axis
			BEGIN
				RETURN 90.0
			END
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 		
		Exec('
			CREATE FUNCTION dbo.XYScaleUnits()
			RETURNS varchar 
			AS 
			-- Returns the scale in the Z axis
			BEGIN
				RETURN ''nm''
			END
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 		
		Exec('
			CREATE FUNCTION dbo.ZScaleUnits()
			RETURNS varchar 
			AS 
			-- Returns the scale in the Z axis
			BEGIN
				RETURN ''nm''
			END
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (39, 
		      N'Add User Defined Functions to define constants such as XY scale' ,getDate(),User_ID())
	 COMMIT TRANSACTION thirtynine
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 40)))
	begin
     print N'Update StructureLocationLinks function' 
	 BEGIN TRANSACTION forty

		Exec('
			ALTER FUNCTION [dbo].[StructureLocationLinks](@StructureID bigint)
			RETURNS TABLE 
			AS
			RETURN(
 					Select LL.* from LocationLink LL
					 join Location L ON L.ID = A
					 where L.ParentID = @StructureID
					 )
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			ALTER PROCEDURE [dbo].[SelectStructureLocationLinks]
				-- Add the parameters for the stored procedure here
				@StructureID bigint
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				-- Insert statements for procedure here
				Select LL.* from LocationLink LL
					 join Location L ON L.ID = A
					 where L.ParentID = @StructureID
	 
			END
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (40, 
		      N'Update StructureLocationLinks function' ,getDate(),User_ID())
	 COMMIT TRANSACTION forty
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 41)))
	begin
     print  N'Added integer_list type'
	 BEGIN TRANSACTION fortyone
		CREATE TYPE integer_list AS TABLE (ID bigint NOT NULL PRIMARY KEY)
		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (41, 
		     N'Added integer_list type' ,getDate(),User_ID())
	 COMMIT TRANSACTION fortyone
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 42)))
	begin
     print  N'Add procedure for selecting network structure IDs'
	 BEGIN TRANSACTION fortytwo
		
		Exec('
			CREATE PROCEDURE [dbo].[SelectNetworkStructureIDs]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN 

				DECLARE @HopSeedCells integer_list
				DECLARE @CellsInNetwork integer_list 

				insert into @HopSeedCells select ID from @IDs 
				insert into @CellsInNetwork select ID from @IDs 

				while @Hops > 0
				BEGIN
					DECLARE @HopSeedCellsChildStructures integer_list
					DECLARE @ChildStructurePartners integer_list
					DECLARE @HopCellsFound integer_list
		
					insert into @HopSeedCellsChildStructures
						select distinct Child.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @HopSeedCells Cells ON Cells.ID = Parent.ID
		
					insert into @ChildStructurePartners
						select distinct SL.TargetID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.SourceID
						UNION
						select distinct SL.SourceID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.TargetID
				 
					insert into @HopCellsFound 
						select distinct Parent.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @ChildStructurePartners Partners ON Partners.ID = Child.ID
						where Parent.ID not in (Select ID from @CellsInNetwork union select ID from @HopSeedCells)
		
					delete S from @HopSeedCells S
		
					insert into @HopSeedCells 
						select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)

					insert into @CellsInNetwork select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)
			 

					delete from @ChildStructurePartners
					delete from @HopCellsFound
			 
					set @Hops = @Hops - 1
				END

				select ID from @CellsInNetwork
			END
			')


		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			CREATE PROCEDURE [dbo].[SelectNetworkDetails]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork exec SelectNetworkStructureIDs @IDs, @Hops

				select S.* from Structure S
					inner join @CellsInNetwork N ON N.ID = S.ID

				select C.* from Structure C
					inner join @CellsInNetwork N ON N.ID = C.ParentID
		
				insert into @ChildrenInNetwork 
					select ChildStruct.ID from Structure S
					inner join @CellsInNetwork N ON S.ID = N.ID
					inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from @ChildrenInNetwork) OR
						  SL.TargetID in (Select ID from @ChildrenInNetwork)
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			CREATE PROCEDURE [dbo].[SelectNetworkStructureLinks]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork exec SelectNetworkStructureIDs @IDs, @Hops

				insert into @ChildrenInNetwork 
					select ChildStruct.ID from Structure S
					inner join @CellsInNetwork N ON S.ID = N.ID
					inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from @ChildrenInNetwork) OR
						  SL.TargetID in (Select ID from @ChildrenInNetwork)
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			CREATE PROCEDURE [dbo].[SelectNetworkStructures]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork exec SelectNetworkStructureIDs @IDs, @Hops

				select S.* from Structure S 
					inner join @CellsInNetwork N ON N.ID = S.ID
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 Exec('
			CREATE PROCEDURE [dbo].[SelectNetworkChildStructureIDs]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork exec SelectNetworkStructureIDs @IDs, @Hops
				
				insert into @ChildrenInNetwork 
					select ChildStruct.ID from Structure S
					inner join @CellsInNetwork N ON S.ID = N.ID
					inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

				select SL.SourceID from StructureLink SL
					where SL.SourceID in (Select ID from @ChildrenInNetwork)
					union
				select SL.TargetID from StructureLink SL
					where SL.TargetID in (Select ID from @ChildrenInNetwork)
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (42, 
		      N'Add procedure for selecting network structure IDs',getDate(),User_ID())
	 COMMIT TRANSACTION fortytwo
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 43)))
	begin
     print  N'Update spatial queries to use mosaic or volume coordinates'
	 BEGIN TRANSACTION fortythree
		
		 IF OBJECT_ID('SelectSectionStructuresAndLinks') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresAndLinks]
		 
		 Exec('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinks]
			-- Add the parameters for the stored procedure here
			@Z float,
			@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionLocations'') IS NOT NULL DROP TABLE #SectionLocations
					select distinct ParentID into #SectionLocations from Location where Z = @Z order by ParentID

					IF @QueryDate IS NOT NULL
						select s.* from Structure s 
							JOIN #SectionLocations l ON (l.ParentID = s.ID)
							where s.LastModified >= @QueryDate
					ELSE
						select s.* from Structure s JOIN #SectionLocations l ON (l.ParentID = s.ID)

					Select * from StructureLink L
					where (L.TargetID in (Select ParentID from #SectionLocations))
						OR (L.SourceID in (Select ParentID from #SectionLocations)) 

					DROP TABLE #SectionLocations
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionStructuresAndLinksInBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresAndLinksInBounds]
		 
		 IF OBJECT_ID('[SelectSectionStructuresAndLinksInMosaicBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresAndLinksInMosaicBounds]
		 
		 Exec('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinksInMosaicBounds]
			-- Add the parameters for the stored procedure here
			@Z float,
			@BBox geometry,
			@MinRadius float,
			@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionIDsInBounds'') IS NOT NULL DROP TABLE #SectionIDsInBounds
					IF OBJECT_ID(''tempdb..#AllSectionStructuresInBounds'') IS NOT NULL DROP TABLE #AllSectionStructuresInBounds
					select S.* into #SectionStructuresInBounds from Structure S
						inner join (Select distinct ParentID from Location where (@bbox.STIntersects(MosaicShape) = 1 and Z = @Z AND Radius >= @MinRadius)) L ON L.ParentID = S.ID
						
					IF @QueryDate IS NOT NULL
						BEGIN
							select SIB.ID into #SectionIDsInBounds from (
								select S.ID as ID from #SectionStructuresInBounds S
									where S.LastModified >= @QueryDate
								union
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLS ON SLS.SourceID = S.ID
									where SLS.LastModified >= @QueryDate
								union 
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLT ON SLT.TargetID = S.ID
									where SLT.LastModified >= @QueryDate ) SIB

							select S.* from #SectionStructuresInBounds S
								inner join #SectionIDsInBounds Modified ON Modified.ID = S.ID

							Select * from StructureLink L
								where (L.TargetID in (Select ID from #SectionIDsInBounds))
									OR (L.SourceID in (Select ID from #SectionIDsInBounds)) 

							DROP TABLE #SectionIDsInBounds
						END
					ELSE
						BEGIN
							select * from #SectionStructuresInBounds

							Select * from StructureLink L
							where (L.TargetID in (Select ID from #SectionStructuresInBounds))
								OR (L.SourceID in (Select ID from #SectionStructuresInBounds)) 
						END
			  
					DROP TABLE #SectionStructuresInBounds
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionStructuresAndLinksInVolumeBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresAndLinksInVolumeBounds]
		 
		 Exec('
			CREATE PROCEDURE [dbo].[SelectSectionStructuresAndLinksInVolumeBounds]
			-- Add the parameters for the stored procedure here
			@Z float,
			@BBox geometry,
			@MinRadius float,
			@QueryDate datetime
			AS
			BEGIN 
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#SectionIDsInBounds'') IS NOT NULL DROP TABLE #SectionIDsInBounds
					IF OBJECT_ID(''tempdb..#AllSectionStructuresInBounds'') IS NOT NULL DROP TABLE #AllSectionStructuresInBounds
					select S.* into #SectionStructuresInBounds from Structure S
						inner join (Select distinct ParentID from Location where (@bbox.STIntersects(VolumeShape) = 1 and Z = @Z AND Radius >= @MinRadius)) L ON L.ParentID = S.ID
						
					IF @QueryDate IS NOT NULL
						BEGIN
							select SIB.ID into #SectionIDsInBounds from (
								select S.ID as ID from #SectionStructuresInBounds S
									where S.LastModified >= @QueryDate
								union
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLS ON SLS.SourceID = S.ID
									where SLS.LastModified >= @QueryDate
								union 
								select S.ID as ID from #SectionStructuresInBounds S
									inner join StructureLink SLT ON SLT.TargetID = S.ID
									where SLT.LastModified >= @QueryDate ) SIB

							select S.* from #SectionStructuresInBounds S
								inner join #SectionIDsInBounds Modified ON Modified.ID = S.ID

							Select * from StructureLink L
								where (L.TargetID in (Select ID from #SectionIDsInBounds))
									OR (L.SourceID in (Select ID from #SectionIDsInBounds)) 

							DROP TABLE #SectionIDsInBounds
						END
					ELSE
						BEGIN
							select * from #SectionStructuresInBounds

							Select * from StructureLink L
							where (L.TargetID in (Select ID from #SectionStructuresInBounds))
								OR (L.SourceID in (Select ID from #SectionStructuresInBounds)) 
						END
			  
					DROP TABLE #SectionStructuresInBounds
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionLocationsAndLinksInBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationsAndLinksInBounds]
		 
		 IF OBJECT_ID('[SelectSectionLocationsAndLinksInMosaicBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationsAndLinksInMosaicBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinksInMosaicBounds]
					-- Add the parameters for the stored procedure here
					@Z float,
					@BBox geometry,
					@Radius float,
					@QueryDate datetime
				AS
				BEGIN
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds

					--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

					select * into #LocationsInBounds FROM Location where Z = @Z AND (@BBox.STIntersects(MosaicShape) = 1) AND Radius >= @Radius order by ID
	 
					IF @QueryDate IS NOT NULL
						Select * from #LocationsInBounds where LastModified >= @QueryDate
					ELSE
						Select * from #LocationsInBounds
	 
					IF @QueryDate IS NOT NULL
						-- Insert statements for procedure here
						Select * from LocationLink
						 WHERE ((A in 
						(select ID from #LocationsInBounds))
						  OR
						  (B in 
						(select ID from #LocationsInBounds)))
						 AND Created >= @QueryDate
					ELSE
						-- Insert statements for procedure here
						Select * from LocationLink
						 WHERE ((A in (select ID from #LocationsInBounds))
								OR	
								(B in (select ID from #LocationsInBounds)))
	
					DROP TABLE #LocationsInBounds
				END 
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionLocationsAndLinksInVolumeBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationsAndLinksInVolumeBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionLocationsAndLinksInVolumeBounds]
					-- Add the parameters for the stored procedure here
					@Z float,
					@BBox geometry,
					@Radius float,
					@QueryDate datetime
				AS
				BEGIN
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds

					--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

					select * into #LocationsInBounds FROM Location where Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @Radius order by ID
	 
					IF @QueryDate IS NOT NULL
						Select * from #LocationsInBounds where LastModified >= @QueryDate
					ELSE
						Select * from #LocationsInBounds
	 
					IF @QueryDate IS NOT NULL
						-- Insert statements for procedure here
						Select * from LocationLink
						 WHERE ((A in 
						(select ID from #LocationsInBounds))
						  OR
						  (B in 
						(select ID from #LocationsInBounds)))
						 AND Created >= @QueryDate
					ELSE
						-- Insert statements for procedure here
						Select * from LocationLink
						 WHERE ((A in (select ID from #LocationsInBounds))
								OR	
								(B in (select ID from #LocationsInBounds)))
	
					DROP TABLE #LocationsInBounds
				END 
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end


		 IF OBJECT_ID('[SelectSectionLocationLinksInBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationLinksInBounds]
		 
		 IF OBJECT_ID('[SelectSectionLocationLinksInMosaicBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationLinksInMosaicBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionLocationLinksInMosaicBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@bbox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				--This really needs to check if a line between the two location links intersects the bounding box.

				IF OBJECT_ID(''tempdb..#LocationsAboveZ'') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID(''tempdb..#LocationsBelowZ'') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z AND (@bbox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID 
				select * into #LocationsBelowZ from Location where Z <= @Z AND (@bbox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID

	
				IF @QueryDate IS NOT NULL
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )))
					 AND Created >= @QueryDate
				ELSE
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 ))) 

				DROP TABLE #LocationsAboveZ
				DROP TABLE #LocationsBelowZ
	 
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionLocationLinksInVolumeBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionLocationLinksInVolumeBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionLocationLinksInVolumeBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@bbox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				--This really needs to check if a line between the two location links intersects the bounding box.

				IF OBJECT_ID(''tempdb..#LocationsAboveZ'') IS NOT NULL DROP TABLE #LocationsAboveZ
				IF OBJECT_ID(''tempdb..#LocationsBelowZ'') IS NOT NULL DROP TABLE #LocationsBelowZ

				--Looks slow, but my tests indicate selecting a single column into the table is slower
				select * into #LocationsAboveZ from Location where Z >= @Z AND (@bbox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID 
				select * into #LocationsBelowZ from Location where Z <= @Z AND (@bbox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID

	
				IF @QueryDate IS NOT NULL
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )))
					 AND Created >= @QueryDate
				ELSE
					Select * from LocationLink
					 WHERE (((A in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsBelowZ)
						 ))
					 OR
						 ((A in
						 (SELECT ID
						  FROM #LocationsBelowZ)
						 )
						  AND
						  (B in 
						(SELECT ID
						  FROM #LocationsAboveZ)
						 ))) 

				DROP TABLE #LocationsAboveZ
				DROP TABLE #LocationsBelowZ
	 
			END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 
		 IF OBJECT_ID('[SelectSectionStructuresInBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresInBounds]
		 
		 IF OBJECT_ID('[SelectSectionStructuresInMosaicBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresInMosaicBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionStructuresInMosaicBounds]
					-- Add the parameters for the stored procedure here
					@Z float,
					@BBox geometry,
					@MinRadius float,
					@QueryDate datetime
				AS
				BEGIN 
						-- SET NOCOUNT ON added to prevent extra result sets from
						-- interfering with SELECT statements.
						SET NOCOUNT ON;

						IF @QueryDate IS NOT NULL
							select S.* from Structure S
								inner join (Select distinct ParentID from Location 
									where (@bbox.STIntersects(MosaicShape) = 1) and Z = @Z AND Radius >= @MinRadius) L 
										ON L.ParentID = S.ID
							WHERE S.LastModified >= @QueryDate
						ELSE
							select S.* from Structure S
								inner join (Select distinct ParentID from Location 
									where (@bbox.STIntersects(MosaicShape) = 1) and Z = @Z AND Radius >= @MinRadius) L 
										ON L.ParentID = S.ID
				END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionStructuresInVolumeBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionStructuresInVolumeBounds]
		 
		 Exec('
				CREATE PROCEDURE [dbo].[SelectSectionStructuresInVolumeBounds]
					-- Add the parameters for the stored procedure here
					@Z float,
					@BBox geometry,
					@MinRadius float,
					@QueryDate datetime
				AS
				BEGIN 
						-- SET NOCOUNT ON added to prevent extra result sets from
						-- interfering with SELECT statements.
						SET NOCOUNT ON;

						IF @QueryDate IS NOT NULL
							select S.* from Structure S
								inner join (Select distinct ParentID from Location 
									where (@bbox.STIntersects(VolumeShape) = 1) and Z = @Z AND Radius >= @MinRadius) L 
										ON L.ParentID = S.ID
							WHERE S.LastModified >= @QueryDate
						ELSE
							select S.* from Structure S
								inner join (Select distinct ParentID from Location 
									where (@bbox.STIntersects(VolumeShape) = 1) and Z = @Z AND Radius >= @MinRadius) L 
										ON L.ParentID = S.ID
				END
			')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionAnnotationsInVolumeBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionAnnotationsInVolumeBounds]
		 
		 Exec('

			 --Return all Structures, StructureLinks, Locations, and LocationLinks in a region
			--If a structure link is modified the host structures in the volume will also be returned
			--If a location link is created the host locations in the bounds will also be returned
			CREATE PROCEDURE [dbo].[SelectSectionAnnotationsInVolumeBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds
				IF OBJECT_ID(''tempdb..#SectionStructureIDsInBounds'') IS NOT NULL DROP TABLE #SectionStructureIDsInBounds
				IF OBJECT_ID(''tempdb..#ModifiedStructuresInBounds'') IS NOT NULL DROP TABLE #ModifiedStructuresInBounds
				IF OBJECT_ID(''tempdb..#ModifiedLocationsInBounds'') IS NOT NULL DROP TABLE #ModifiedLocationsInBounds

				--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

				select * into #LocationsInBounds FROM Location 
					WHERE Z = @Z AND (@BBox.STIntersects(VolumeShape) = 1) AND Radius >= @MinRadius order by ID

				select distinct L.ParentID as ID into #SectionStructureIDsInBounds from #LocationsInBounds L
								 
				IF @QueryDate IS NOT NULL
					BEGIN
						--Grab all structures who have had a link or location in the region updated. 
						--This ensures each location in the region has a structure
						select SIB.ID into #ModifiedStructuresInBounds from (
							select S.ID as ID from Structure S
								inner join #SectionStructureIDsInBounds SIB ON SIB.ID  = S.ID
									where S.LastModified >= @QueryDate
							union
							select S.ID as ID from #SectionStructureIDsInBounds S
								inner join StructureLink SLS ON SLS.SourceID = S.ID
								where SLS.LastModified >= @QueryDate
							union 
							select S.ID as ID from #SectionStructureIDsInBounds S
								inner join StructureLink SLT ON SLT.TargetID = S.ID
								where SLT.LastModified >= @QueryDate ) SIB


						select * from Structure S
							inner join #ModifiedStructuresInBounds Modified ON Modified.ID = S.ID

						Select * from StructureLink L
							where (L.TargetID in (Select ID from #ModifiedStructuresInBounds))
								OR (L.SourceID in (Select ID from #ModifiedStructuresInBounds)) 

						select ML.ID into #ModifiedLocationsInBounds from (
							select L.ID from #LocationsInBounds L
								where L.LastModified >= @QueryDate
							UNION
							select L.ID from #LocationsInBounds L
								inner join LocationLink LL ON LL.A = L.ID
									where LL.Created >= @QueryDate
							UNION
							select L.ID from #LocationsInBounds L
								inner join LocationLink LL ON LL.B = L.ID
									where LL.Created >= @QueryDate
						) ML

						Select * from Location L	
							inner join #ModifiedLocationsInBounds MLIB ON MLIB.ID = L.ID 

						Select * from LocationLink
							WHERE ((A in (select ID from #ModifiedLocationsInBounds))
								OR	
								   (B in (select ID from #ModifiedLocationsInBounds)))

						DROP TABLE #ModifiedStructuresInBounds
						DROP TABLE #ModifiedLocationsInBounds
					END
				ELSE
					BEGIN
						select S.* from Structure S
							inner join #SectionStructureIDsInBounds SIB ON SIB.ID = S.ID

						Select * from StructureLink L
							where (L.TargetID in (Select ID from #SectionStructureIDsInBounds))
								OR (L.SourceID in (Select ID from #SectionStructureIDsInBounds)) 

						Select * from Location L 
							inner join #LocationsInBounds LIB ON LIB.ID = L.ID

						Select * from LocationLink
							WHERE ((A in (select ID from #LocationsInBounds))
								OR	
								   (B in (select ID from #LocationsInBounds)))
					END
	
				DROP TABLE #LocationsInBounds
				DROP TABLE #SectionStructureIDsInBounds
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 IF OBJECT_ID('[SelectSectionAnnotationsInMosaicBounds]') IS NOT NULL DROP PROCEDURE dbo.[SelectSectionAnnotationsInMosaicBounds]
		 
		 Exec('

			 --Return all Structures, StructureLinks, Locations, and LocationLinks in a region
			--If a structure link is modified the host structures in the volume will also be returned
			--If a location link is created the host locations in the bounds will also be returned
			CREATE PROCEDURE [dbo].[SelectSectionAnnotationsInMosaicBounds]
				-- Add the parameters for the stored procedure here
				@Z float,
				@BBox geometry,
				@MinRadius float,
				@QueryDate datetime
			AS
			BEGIN
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;

				IF OBJECT_ID(''tempdb..#LocationsInBounds'') IS NOT NULL DROP TABLE #LocationsInBounds
				IF OBJECT_ID(''tempdb..#SectionStructureIDsInBounds'') IS NOT NULL DROP TABLE #SectionStructureIDsInBounds
				IF OBJECT_ID(''tempdb..#ModifiedStructuresInBounds'') IS NOT NULL DROP TABLE #ModifiedStructuresInBounds
				IF OBJECT_ID(''tempdb..#ModifiedLocationsInBounds'') IS NOT NULL DROP TABLE #ModifiedLocationsInBounds

				--Selecting all columns once into LocationsInBounds and then selecting the temp table is a huge time saver.  3-4 seconds instead of 20.

				select * into #LocationsInBounds FROM Location 
					WHERE Z = @Z AND (@BBox.STIntersects(MosaicShape) = 1) AND Radius >= @MinRadius order by ID

				select distinct L.ParentID as ID into #SectionStructureIDsInBounds from #LocationsInBounds L
								 
				IF @QueryDate IS NOT NULL
					BEGIN
						--Grab all structures who have had a link or location in the region updated. 
						--This ensures each location in the region has a structure
						select SIB.ID into #ModifiedStructuresInBounds from (
							select S.ID as ID from Structure S
								inner join #SectionStructureIDsInBounds SIB ON SIB.ID  = S.ID
									where S.LastModified >= @QueryDate
							union
							select S.ID as ID from #SectionStructureIDsInBounds S
								inner join StructureLink SLS ON SLS.SourceID = S.ID
								where SLS.LastModified >= @QueryDate
							union 
							select S.ID as ID from #SectionStructureIDsInBounds S
								inner join StructureLink SLT ON SLT.TargetID = S.ID
								where SLT.LastModified >= @QueryDate ) SIB


						select * from Structure S
							inner join #ModifiedStructuresInBounds Modified ON Modified.ID = S.ID

						Select * from StructureLink L
							where (L.TargetID in (Select ID from #ModifiedStructuresInBounds))
								OR (L.SourceID in (Select ID from #ModifiedStructuresInBounds)) 

						select ML.ID into #ModifiedLocationsInBounds from (
							select L.ID from #LocationsInBounds L
								where L.LastModified >= @QueryDate
							UNION
							select L.ID from #LocationsInBounds L
								inner join LocationLink LL ON LL.A = L.ID
									where LL.Created >= @QueryDate
							UNION
							select L.ID from #LocationsInBounds L
								inner join LocationLink LL ON LL.B = L.ID
									where LL.Created >= @QueryDate
						) ML

						Select * from Location L	
							inner join #ModifiedLocationsInBounds MLIB ON MLIB.ID = L.ID 

						Select * from LocationLink
							WHERE ((A in (select ID from #ModifiedLocationsInBounds))
								OR	
								   (B in (select ID from #ModifiedLocationsInBounds)))

						DROP TABLE #ModifiedStructuresInBounds
						DROP TABLE #ModifiedLocationsInBounds
					END
				ELSE
					BEGIN
						select S.* from Structure S
							inner join #SectionStructureIDsInBounds SIB ON SIB.ID = S.ID

						Select * from StructureLink L
							where (L.TargetID in (Select ID from #SectionStructureIDsInBounds))
								OR (L.SourceID in (Select ID from #SectionStructureIDsInBounds)) 

						Select * from Location L 
							inner join #LocationsInBounds LIB ON LIB.ID = L.ID

						Select * from LocationLink
							WHERE ((A in (select ID from #LocationsInBounds))
								OR	
								   (B in (select ID from #LocationsInBounds)))
					END
	
				DROP TABLE #LocationsInBounds
				DROP TABLE #SectionStructureIDsInBounds
			END 
		')

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end


	 INSERT INTO DBVersion values (43, 
		      N'Update spatial queries to use mosaic or volume coordinates',getDate(),User_ID())
	 COMMIT TRANSACTION fortythree
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 44)))
	begin
     print N'Create role for annotation power user'
     BEGIN TRANSACTION fortyfour

		IF DATABASE_PRINCIPAL_ID('role') IS NULL
		begin
			CREATE ROLE [AnnotationPowerUser]
		end

		GRANT EXECUTE ON UpdateStructureType TO [AnnotationPowerUser]
		GRANT VIEW DEFINITION ON UpdateStructureType TO [AnnotationPowerUser]

		if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (44, 
		   N'Create role for annotation power user',getDate(),User_ID())

	 COMMIT TRANSACTION fortyfour
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 45)))
	begin
     print N'Repeate update triggers to use more precise SYSUTCDATETIME() function.  This was repeated because two patches used #20.'
     BEGIN TRANSACTION fortyfive
		IF EXISTS(select * from sys.triggers where name = 'Location_update')
		BEGIN
			DROP TRIGGER [dbo].[Location_update];
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
					Set LastModified = (SYSUTCDATETIME())
					WHERE ID in (SELECT ID FROM inserted)
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;');
				
		EXEC('CREATE TRIGGER [dbo].[Structure_LastModified] 
			   ON  [dbo].[Structure]
			   FOR UPDATE
			AS 
				Update dbo.[Structure]
				Set LastModified = (SYSUTCDATETIME())
				WHERE ID in (SELECT ID FROM inserted)
				-- SET NOCOUNT ON added to prevent extra result sets from
				-- interfering with SELECT statements.
				SET NOCOUNT ON;');
				
		EXEC('CREATE TRIGGER [dbo].[StructureType_LastModified] 
			   ON  [dbo].[StructureType]
			   FOR UPDATE
			AS 
				Update dbo.[StructureType]
				Set LastModified = (SYSUTCDATETIME())
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
		 INSERT INTO DBVersion values (45, 
		   'Repeate update triggers to use more precise SYSUTCDATETIME() function.  This was repeated because two patches used #20.',getDate(),User_ID())

	 COMMIT TRANSACTION fortyfive
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 46)))
	begin
     print N'Add geometry functions used for spatial migration'
     BEGIN TRANSACTION fortysix
		 
		 		IF OBJECT_ID (N'dbo.ufnTranslatePoint', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnTranslatePoint;
				
				EXEC('
				CREATE FUNCTION dbo.ufnTranslatePoint(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return POINT S translated by values in Point T
				BEGIN
					DECLARE @XNudge float
					DECLARE @YNudge float
	
					return geometry::Point(@S.STX + @T.STX, @S.STY + @T.STY, @S.STSrid)
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end
				

				IF OBJECT_ID (N'dbo.ufnVector', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnVector;
				
				EXEC('
				CREATE FUNCTION dbo.ufnVector(@Angle float, @Magnitude float)
				RETURNS geometry 
				AS 
				--Return A vector from origin 0,0 at Angle with magnitude M
				BEGIN
					return geometry::Point(COS(@Angle) * @Magnitude,
										   SIN(@Angle) * @Magnitude, 0)
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnTriangleArea', N'FN') IS NOT NULL
					DROP FUNCTION ufnTriangleArea;
				
				EXEC('
				CREATE FUNCTION dbo.ufnTriangleArea(@P1 geometry, @P2 geometry, @P3 geometry)
				RETURNS float 
				AS 
				-- Returns the stock level for the product.
				BEGIN
					DECLARE @ret float
					DECLARE @S float
					DECLARE @A float
					DECLARE @B float
					DECLARE @C float
					set @A = @P1.STDistance(@P2)
					set @B = @P2.STDistance(@P3)
					set @C = @P3.STDistance(@P1)
					set @S = (@A + @B + @C) / 2.0
					set @ret = SQRT(@S * (@S - @A) * (@S - @B) * (@S - @C))
					RETURN @ret;
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnAngleBetweenShapes', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnAngleBetweenShapes;
				
				EXEC('
				CREATE FUNCTION [dbo].[ufnAngleBetweenShapes](@S geometry, @T geometry)
					RETURNS float 
					AS 
					-- Returns a line where two circles intersect.  
					-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
					BEGIN
						DECLARE @Angle float 

						DECLARE @SCenter geometry
						DECLARE @TCenter geometry
						set @SCenter = @S.STCentroid ( )
						set @TCenter = @T.STCentroid ( )
						set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)
						RETURN @Angle
					END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromPoints', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromPoints;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromPoints(@P1 geometry, @P2 geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
					if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '' '' +
																   STR(@P1.Z, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '' '' +
																   STR(@P2.Z, 10,8) + '')'',0)
					ELSE
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '')'',0)
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromThreePoints', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromThreePoints;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromThreePoints(@P1 geometry, @P2 geometry, @P3 geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
					if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '' '' +
																   STR(@P1.Z, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '' '' +
																   STR(@P2.Z, 10,8) + '', '' +
																   STR(@P3.STX, 10,8) + '' '' +
																   STR(@P3.STY, 10,8) + '' '' +
																   STR(@P3.Z, 10,8) + '')'',0)
					ELSE
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '', '' + 
																   STR(@P3.STX, 10,8) + '' '' +
																   STR(@P3.STY, 10,8) + '')'',0)
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromAngleAndDistance', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromAngleAndDistance;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromAngleAndDistance(@Angle float, @distance float, @offset geometry)
				RETURNS geometry
				AS 
				-- Returns a line centered on offset with @angle and total length = @distance
				BEGIN
					DECLARE @ret geometry
					DECLARE @P1X float
					DECLARE @P1Y float
					DECLARE @P2X float
					DECLARE @P2Y float
					DECLARE @Radius float
					DECLARE @Tau float
					set @Radius = @distance / 2.0 

					--Need to create a line centered on 0,0 so we can translate it to the center of S
					set @P1X = (COS(@Angle - PI()) * @Radius) + @offset.STX
					set @P1Y = (SIN(@Angle - PI()) * @Radius) + @offset.STY
					set @P2X = (COS(@Angle) * @Radius) + @offset.STX
					set @P2Y = (SIN(@Angle) * @Radius) + @offset.STY

					if @Offset.Z is NOT NULL
						set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
																STR(@P1Y, 10,8) + '' '' +
																STR(@offset.Z, 10, 8) + '', '' + 
																STR(@P2X, 10,8) + '' '' +
																STR(@P2Y, 10,8) + '' '' +
																STR(@offset.Z, 10, 8) + '')'',0)
					ELSE
						set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
																STR(@P1Y, 10,8) + '', '' + 
																STR(@P2X, 10,8) + '' '' +
																STR(@P2Y, 10,8) + '')'',0)
				  
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromLinkedShapes', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromLinkedShapes;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromLinkedShapes(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
	
					IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
					BEGIN
						DECLARE @Points geometry
						SET @Points = @S.STBoundary().STIntersection(@T.STBoundary())
						SET @ret = DBO.ufnLineFromPoints(@Points.STGeometryN(1), @Points.STGeometryN(2))
					END
					ELSE
					BEGIN
						DECLARE @SCenter geometry
						DECLARE @TCenter geometry
						DECLARE @Radius float
						DECLARE @Angle float
						set @SCenter = @S.STCentroid ( )
						set @TCenter = @T.STCentroid ( )
						set @Radius = SQRT(@T.STArea() / PI())
						set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX) + (PI() / 2.0)
		
						set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @TCenter)
					END
			 
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnCreateCircle', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnCreateCircle;
				
				EXEC('
				CREATE FUNCTION dbo.ufnCreateCircle(@C geometry, @Radius float)
				RETURNS geometry 
				AS 
				---Create a circle at point C with radius
				BEGIN
					declare @MinX float
					declare @MinY float
					declare @MaxX float
					declare @MaxY float

					set @MinX = @C.STX - @Radius
					set @MinY = @C.STY - @Radius
					set @MaxX = @C.STX + @Radius
					set @MaxY = @C.STY + @Radius

					RETURN geometry::STGeomFromText(''CURVEPOLYGON( CIRCULARSTRING(   ''+ STR(@MinX,16,2) + '' '' + STR(@C.STY,16,2) + '','' +
																		   + STR(@C.STX,16,2) + '' '' + STR(@MaxY,16,2) + '','' +
																		   + STR(@MaxX,16,2) + '' '' + STR(@C.STY,16,2) + '','' +
																		   + STR(@C.STX,16,2) + '' '' + STR(@MinY,16,2) + '','' +
																		   + STR(@MinX,16,2) + '' '' + STR(@C.STY,16,2) + '' ))'', 0);
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromThreePoints', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromThreePoints;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromThreePoints(@P1 geometry, @P2 geometry, @P3 geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
					if @P1.Z IS NOT NULL AND @P2.Z IS NOT NULL
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '' '' +
																   STR(@P1.Z, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '' '' +
																   STR(@P2.Z, 10,8) + '', '' +
																   STR(@P3.STX, 10,8) + '' '' +
																   STR(@P3.STY, 10,8) + '' '' +
																   STR(@P3.Z, 10,8) + '')'',0)
					ELSE
						SET @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1.STX, 10,8) + '' '' +
																   STR(@P1.STY, 10,8) + '', '' +
																   STR(@P2.STX, 10,8) + '' '' +
																   STR(@P2.STY, 10,8) + '', '' + 
																   STR(@P3.STX, 10,8) + '' '' +
																   STR(@P3.STY, 10,8) + '')'',0)
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnWeightedMidpointBetweenCircles', N'FN') IS NOT NULL
					DROP FUNCTION ufnWeightedMidpointBetweenCircles;
				
				EXEC('
				CREATE FUNCTION dbo.ufnWeightedMidpointBetweenCircles(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
					--We are trying to find the point between two circles where the distances, normalized by ratio,
					-- from the centers to point P are equal.  We call this normalized value Z1 & Z2 for Circle 1 & 2
					-- Z should be from 0 to 1 for each circle.  If it is not we return NULL.  
					-- There are two possible Z values.  If both fall within 0 to 1 we take the one between the circles
				BEGIN
					DECLARE @ret geometry
					DECLARE @Distance float
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @SRadius float
					DECLARE @TRadius float
					DECLARE @Angle float
					DECLARE @X1_MID float
					DECLARE @Y1_MID float

					DECLARE @X2_MID float
					DECLARE @Y2_MID float

					DECLARE @RadiusDiff float
					DECLARE @RadiusSum float
					DECLARE @RadiusRatio float

					DECLARE @Z1 float
					DECLARE @Z2 float

					DECLARE @S_MID_DIST1 float
					DECLARE @T_MID_DIST1 float
					DECLARE @S_MID_DIST2 float
					DECLARE @T_MID_DIST2 float

					set @SCenter = @S.STCentroid ( )
					set @TCenter = @T.STCentroid ( )
					set @SRadius = SQRT(@S.STArea() / PI())
					set @TRadius = SQRT(@T.STArea() / PI())

					set @RadiusDiff = @TRadius - @SRadius
					set @RadiusSum = @TRadius + @SRadius

					IF @RadiusDiff = 0 BEGIN
						return geometry::Point((@SCenter.STX + @TCenter.STX) / 2.0,
											   (@SCenter.STY + @TCenter.STY) / 2.0,
											   0)
					END

					--There are two possible midpoints
					set @X1_MID = ((-@SRadius * @TCenter.STX) / @RadiusDiff) + ((@TRadius * @SCenter.STX) / @RadiusDiff)
					set @X2_MID = ((@SRadius * @TCenter.STX) / @RadiusSum) + ((@TRadius * @SCenter.STX) / @RadiusSum)

					set @Y1_MID = ((-@SRadius * @TCenter.STY) / @RadiusDiff) + ((@TRadius * @SCenter.STY) / @RadiusDiff)
					set @Y2_MID = ((@SRadius * @TCenter.STY) / @RadiusSum) + ((@TRadius * @SCenter.STY) / @RadiusSum)


					set @S_MID_DIST1 = SQRT(POWER(@X1_MID - @SCenter.STX,2) + POWER(@Y1_MID - @SCenter.STY,2))
					set @S_MID_DIST2 = SQRT(POWER(@X2_MID - @SCenter.STX,2) + POWER(@Y2_MID - @SCenter.STY,2))

					--set @T_MID_DIST1 = SQRT(POWER(@X1_MID - @TCenter.STX,2) + POWER(@Y1_MID - @TCenter.STY,2))
					--set @T_MID_DIST2 = SQRT(POWER(@X2_MID - @TCenter.STX,2) + POWER(@Y2_MID - @TCenter.STY,2))

					set @Z1 = @S_MID_DIST1 / @SRadius
					set @Z2 = @S_MID_DIST2 / @SRadius

					IF(@Z1 > 1.0 AND @Z2 > 1.0)
						return NULL
	
					IF(@Z1 <= @Z2)
						return geometry::Point(@X1_MID, @Y1_MID, 0)
					ELSE
						return geometry::Point(@X2_MID, @Y2_MID, 0)
	
					--set @Angle = dbo.ufnAngleBetweenShapes(@S,@T)
					--set @Distance = @SCenter.STDistance(@TCenter)

					RETURN @ret
				END

				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnIntersectingCurveForCircles', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnIntersectingCurveForCircles;

				EXEC('
				CREATE FUNCTION dbo.ufnIntersectingCurveForCircles(@S geometry, @T geometry, @SeperateDistance float)
				RETURNS geometry 
				AS 
					--Returns a three part line that passes through the points where the two circles intersect and the weighted
					--midpoint between the circles
					--@SeperateDistance Nudges the line away from the target geometry by a set distance.  Used to prevent lines from 
					--perfectly overlapping when migrating linked source and target locations
				BEGIN
					DECLARE @ret geometry
					DECLARE @Points geometry
					DECLARE @Midpoint geometry
					DECLARE @SBounds geometry
					DECLARE @TBounds geometry

					IF(@S.STDimension() > 1)
						set @SBounds = @S.STBoundary() 
					ELSE
						set @SBounds = @S

					IF(@T.STDimension() > 1)
						set @TBounds = @T.STBoundary() 
					ELSE
						set @TBounds = @T

					SET @Points = @SBounds.STIntersection(@TBounds)
					SET @Midpoint = dbo.ufnWeightedMidpointBetweenCircles(@S, @T)

					DECLARE @Startpoint geometry
					DECLARE @Endpoint geometry
					set @Startpoint = @Points.STGeometryN(1)
					set @Endpoint = @Points.STGeometryN(2)
	
					IF @SeperateDistance IS NOT NULL
					BEGIN
						DECLARE @Angle float
						DECLARE @TranslateVector geometry
						set @Angle = dbo.ufnAngleBetweenShapes(@S,@T)
						set @TranslateVector = dbo.ufnVector(@Angle, @SeperateDistance)
						set @Startpoint = dbo.ufnTranslatePoint(@Startpoint, @TranslateVector)
						set @Endpoint = dbo.ufnTranslatePoint(@Endpoint, @TranslateVector)
						set @Midpoint = dbo.ufnTranslatePoint(@Midpoint, @TranslateVector)

					END

					SET @ret = dbo.ufnLineFromThreePoints(@Startpoint, @Midpoint, @Endpoint)
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineFromAngleAndDistance', N'FN') IS NOT NULL
					DROP FUNCTION ufnLineFromAngleAndDistance;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineFromAngleAndDistance(@Angle float, @distance float, @offset geometry)
				RETURNS geometry
				AS 
				-- Returns a line centered on offset with @angle and total length = @distance
				BEGIN
					DECLARE @ret geometry
					DECLARE @P1X float
					DECLARE @P1Y float
					DECLARE @P2X float
					DECLARE @P2Y float
					DECLARE @Radius float
					DECLARE @Tau float
					set @Radius = @distance / 2.0 

					--Need to create a line centered on 0,0 so we can translate it to the center of S
					set @P1X = (COS(@Angle - PI()) * @Radius) + @offset.STX
					set @P1Y = (SIN(@Angle - PI()) * @Radius) + @offset.STY
					set @P2X = (COS(@Angle) * @Radius) + @offset.STX
					set @P2Y = (SIN(@Angle) * @Radius) + @offset.STY

					if @Offset.Z is NOT NULL
						set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
																STR(@P1Y, 10,8) + '' '' +
																STR(@offset.Z, 10, 8) + '', '' + 
																STR(@P2X, 10,8) + '' '' +
																STR(@P2Y, 10,8) + '' '' +
																STR(@offset.Z, 10, 8) + '')'',0)
					ELSE
						set @ret = geometry::STLineFromText( ''LINESTRING ( '' + STR(@P1X, 10,8)  + '' '' +
																STR(@P1Y, 10,8) + '', '' + 
																STR(@P2X, 10,8) + '' '' +
																STR(@P2Y, 10,8) + '')'',0)
				  
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnLineThroughCircle', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnLineThroughCircle;
				
				EXEC('
				CREATE FUNCTION dbo.ufnLineThroughCircle(@S geometry, @T geometry, @Perpendicular bit)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S perpendicular to target point @T
				BEGIN
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @ret geometry
					DECLARE @Radius float
					DECLARE @Angle float
					set @SCenter = @S.STCentroid ( )
					set @TCenter = @T.STCentroid ( )
					set @Radius = SQRT(@S.STArea() / PI())
					set @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)
					IF @Perpendicular = 1
						set @Angle = @Angle + (PI() / 2.0)
		
					set @ret = dbo.ufnLineFromAngleAndDistance( @Angle, @Radius * 2, @SCenter)
					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end


				IF OBJECT_ID (N'dbo.ufnPerpendicularLineThroughCircle', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnPerpendicularLineThroughCircle;
				
				EXEC('
				CREATE FUNCTION dbo.ufnPerpendicularLineThroughCircle(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S perpendicular to target point @T
				BEGIN
					RETURN dbo.ufnLineThroughCircle(@S,@T,1)
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end


				IF OBJECT_ID (N'dbo.ufnParallelLineThroughCircle', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnParallelLineThroughCircle;
				
				EXEC('
				CREATE FUNCTION dbo.ufnParallelLineThroughCircle(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				--Return a line passing through the center of circle S towards target point @T
				BEGIN
					RETURN dbo.ufnLineThroughCircle(@S,@T,0)
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end

				IF OBJECT_ID (N'dbo.ufnPerpendicularLineToIntersectionPointOfCircles', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnPerpendicularLineToIntersectionPointOfCircles;
				
				EXEC('
				CREATE FUNCTION dbo.ufnPerpendicularLineToIntersectionPointOfCircles(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				-- Return a line that passes from the edge of circle S, through the center of S, and terminates at the midpoint between S AND T
				BEGIN
					DECLARE @ret geometry
					DECLARE @SCenter geometry
					DECLARE @TCenter geometry
					DECLARE @Midpoint geometry
					DECLARE @Edgepoint geometry
					DECLARE @Angle float
					DECLARE @SRadius float	
					SET @SCenter = @S.STCentroid()
					SET @TCenter = @T.STCentroid()
					SET @Midpoint = dbo.ufnWeightedMidpointBetweenCircles(@S, @T)
					SET @SRadius = SQRT(@S.STArea() / PI())

					if @SCenter.STX = @Midpoint.STX AND @SCenter.STY = @Midpoint.STY 
						return @SCenter

					SET @Angle = ATN2(@SCenter.STY - @TCenter.STY, @SCenter.STX - @TCenter.STX)

					--If Midpoint is NULL it means the circles do not overlap
					IF (@Midpoint IS NULL)
						return dbo.ufnLineFromAngleAndDistance(@Angle, @SRadius * 2, @SCenter)
	 
					SET @Edgepoint = dbo.ufnTranslatePoint(dbo.ufnVector(@Angle, @SRadius), @SCenter);
					SET @ret = dbo.ufnLineFromPoints(@Edgepoint, @Midpoint);
					return @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN
				end
		

				IF OBJECT_ID (N'dbo.ufnParallelLineForLinkedShapes', N'FN') IS NOT NULL
					DROP FUNCTION dbo.ufnParallelLineForLinkedShapes;
				
				EXEC('

				CREATE FUNCTION dbo.ufnParallelLineForLinkedShapes(@S geometry, @T geometry)
					RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
	
					IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
					BEGIN
						set @ret = dbo.ufnIntersectingCurveForCircles(@S,@T, 8.0)
					END
					ELSE
					BEGIN
						set @ret = dbo.ufnParallelLineThroughCircle(@S, @T)
					END

					RETURN @ret
				END
				');

				if(@@error <> 0)
				begin
				ROLLBACK TRANSACTION 
				RETURN 
				end

				IF OBJECT_ID (N'dbo.ufnPerpendicularLineForLinkedShapes', N'FN') IS NOT NULL
					DROP FUNCTION ufnPerpendicularLineForLinkedShapes;
				
				EXEC('
				CREATE FUNCTION dbo.ufnPerpendicularLineForLinkedShapes(@S geometry, @T geometry)
				RETURNS geometry 
				AS 
				-- Returns a line where two circles intersect.  
				-- If they do not intersect returns a line that is perpendicular to a direct line between two shapes.  Centered on T.
				BEGIN
					DECLARE @ret geometry
	
					IF @T.STIntersects(@S) = 1 AND @S.STContains(@T) = 0 AND @T.STContains(@S) = 0
					BEGIN
						set @ret = dbo.ufnPerpendicularLineToIntersectionPointOfCircles(@S,@T)
					END
					ELSE
					BEGIN
						set @ret = dbo.ufnPerpendicularLineThroughCircle(@S,@T)
					END
			 
					RETURN @ret
				END
				');

		--any potential errors get reported, and the script is rolled back and terminated
		 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (46, 
		   N'Add geometry functions used for spatial migration',getDate(),User_ID())

	 COMMIT TRANSACTION fortysix
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 47)))
	begin
     print N'Convert Z to integer from double'
     BEGIN TRANSACTION fortyseven

	 ------------------------------ Alter Z to an integer column --------------------------------------------------------

		DROP STATISTICS [Location].[_dta_stat_Location_ParentID_ID_Z], [Location].[_dta_stat_Location_Z_ID]
		DROP INDEX Z on dbo.Location

		ALTER TABLE dbo.Location ALTER COLUMN Z bigint NOT NULL
		

		CREATE NONCLUSTERED INDEX [Z] ON [dbo].[Location] 
			(
				[Z] ASC
			)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
		

		CREATE STATISTICS [_dta_stat_Location_ParentID_ID_Z] ON [dbo].[Location]([ParentID], [ID], [Z])
		CREATE STATISTICS [_dta_stat_Location_Z_ID] ON [dbo].[Location]([Z], [ID])
		 
	 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (47, 
		   N'Convert Z to integer from double',getDate(),User_ID())

	 COMMIT TRANSACTION fortyseven
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 48)))
	begin
     print N'Grant permissions on integer_list'
     BEGIN TRANSACTION fortyeight

	 Grant EXECUTE on TYPE::integer_list to public  

	 exec('CREATE FUNCTION TypeIDToName
		(
			-- Add the parameters for the function here
			@ID bigint 
		)
		RETURNS nvarchar(128)
		BEGIN
			-- Declare the return variable here
			DECLARE @Retval nvarchar(128)

			-- Add the T-SQL statements to compute the return value here
			SELECT top 1 @Retval = Name from StructureType ST where ST.ID = @ID

			-- Return the result of the function
			RETURN @Retval

		END')

		Grant EXECUTE on TypeIDToName to public  

	 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (48, 
		   N'Grant permissions on integer_list',getDate(),User_ID())

	 COMMIT TRANSACTION fortyeight
	 end

	if(not(exists(select (1) from DBVersion where DBVersionID = 49)))
	begin
     print N'Calculate VolumeX/Y correctly for LineStrings'
     BEGIN TRANSACTION fortynine
	  
	  ALTER TABLE Location DROP COLUMN VolumeX 
	  ALTER TABLE Location ADD VolumeX as ISNULL(VolumeShape.STCentroid().STX, ISNULL(VolumeShape.STX, ISNULL(VolumeShape.STEnvelope().STCentroid().STX, 0))) PERSISTED
	  ALTER TABLE Location DROP COLUMN VolumeY
	  ALTER TABLE Location ADD VolumeY as ISNULL(VolumeShape.STCentroid().STY, ISNULL(VolumeShape.STY, ISNULL(VolumeShape.STEnvelope().STCentroid().STY, 0))) PERSISTED
	   
	 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		  --insert the second version marker
		 INSERT INTO DBVersion values (49, 
		   N'Calculate VolumeX/Y correctly for LineStrings',getDate(),User_ID())

	 COMMIT TRANSACTION fortynine
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 50)))
	begin
     print N'Add width column'
     BEGIN TRANSACTION fifty
	   
	  -- ALTER TABLE Location DROP COLUMN Width 
	  ALTER TABLE Location ADD Width float NULL

	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
		 
		  --insert the second version marker
		 INSERT INTO DBVersion values (50, 
		   N'Add width column',getDate(),User_ID())

	 COMMIT TRANSACTION fifty
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 51)))
	begin
     print N'Convert Radius to computed column and add a new width property for use with lines'
     BEGIN TRANSACTION fiftyone 

	 EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Width used for line annotation types' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Width'
	  
	 --ALTER TABLE Location DROP CONSTRAINT chk_Location_Width

	 UPDATE Location SET Width = Radius FROM Location WHERE TypeCode != 1
	 
	 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
	 
	 ALTER TABLE Location
		ADD CONSTRAINT chk_Location_Width CHECK (
			(TypeCode = 1 AND Width IS NULL) OR
			(TypeCode != 1 AND Width IS NOT NULL)
		) 

	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

      ALTER TABLE Location DROP CONSTRAINT DF_Location_Radius

	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	  ALTER TABLE Location DROP COLUMN Radius

	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end
	  
	  ALTER TABLE Location ADD Radius as  
		CASE MosaicShape.STDimension()
			WHEN 0 THEN 0
			WHEN 1 THEN MosaicShape.STLength() / 2.0
			WHEN 2 THEN SQRT( MosaicShape.STArea() / PI() )
		END PERSISTED NOT NULL

	 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Radius, calculated column needed for backwards compatability' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Location', @level2type=N'COLUMN',@level2name=N'Radius'

		  --insert the second version marker
		 INSERT INTO DBVersion values (51, 
		   N'Convert Radius to computed column and add a new width property for use with lines',getDate(),User_ID())

	 COMMIT TRANSACTION fiftyone
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 52)))
	begin
     print N'Add table listing which structures are allowed to be linked'
     BEGIN TRANSACTION fiftytwo
	   
	   -- ALTER TABLE Location DROP COLUMN Width 
	  CREATE TABLE [dbo].[PermittedStructureLink](
		[SourceTypeID] [bigint] NOT NULL,
		[TargetTypeID] [bigint] NOT NULL,
		[Bidirectional] [BIT] NOT NULL,
		CONSTRAINT [PK_PermittedStructureLink] PRIMARY KEY CLUSTERED 
	(
		[SourceTypeID] ASC,
		[TargetTypeID] ASC
	)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
	) ON [PRIMARY]

	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	ALTER TABLE [dbo].[PermittedStructureLink]  WITH CHECK ADD  CONSTRAINT [FK_PermittedStructureLink_SourceType] FOREIGN KEY([SourceTypeID])
	REFERENCES [dbo].[StructureType] ([ID])

	ALTER TABLE [dbo].[PermittedStructureLink]  WITH CHECK ADD  CONSTRAINT [FK_PermittedStructureLink_TargetType] FOREIGN KEY([TargetTypeID])
	REFERENCES [dbo].[StructureType] ([ID])


		  --insert the second version marker
		 INSERT INTO DBVersion values (52, 
		   N'Add table listing which structures are allowed to be linked',getDate(),User_ID())

	 COMMIT TRANSACTION fiftytwo
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 53)))
	begin
     print N'Grant permissions on spatial user-defined-functions'
     BEGIN TRANSACTION fiftythree

	 Grant EXECUTE on ufnAngleBetweenShapes to public
	 Grant EXECUTE on ufnCreateCircle to public
	 Grant EXECUTE on ufnIntersectingCurveForCircles to public
	 Grant EXECUTE on ufnLineFromAngleAndDistance to public
	 Grant EXECUTE on ufnLineFromLinkedShapes to public
	 Grant EXECUTE on ufnLineFromPoints to public
	 Grant EXECUTE on ufnLineFromLinkedShapes to public
	 Grant EXECUTE on ufnLineFromThreePoints to public
	 Grant EXECUTE on ufnLineThroughCircle to public
	 Grant EXECUTE on ufnParallelLineForLinkedShapes to public
	 Grant EXECUTE on ufnParallelLineThroughCircle to public
	 Grant EXECUTE on ufnPerpendicularLineForLinkedShapes to public
	 Grant EXECUTE on ufnPerpendicularLineThroughCircle to public
	 Grant EXECUTE on ufnPerpendicularLineToIntersectionPointOfCircles to public
	 Grant EXECUTE on ufnTranslatePoint to public
	 Grant EXECUTE on ufnTriangleArea to public
	 Grant EXECUTE on ufnVector to public
	 Grant EXECUTE on ufnWeightedMidpointBetweenCircles to public
	 Grant EXECUTE on XYScale to public
	 Grant EXECUTE on XYScaleUnits to public
	 Grant EXECUTE on ZScale to public
	 Grant EXECUTE on ZScaleUnits to public

	 Grant EXECUTE on SelectStructure to public
	 Grant EXECUTE on SelectStructureLocations to public
	 Grant EXECUTE on SelectStructureLocationLinks to public
	 Grant EXECUTE on SelectAllStructures to public
	 Grant EXECUTE on SelectAllStructureLocations to public
	 Grant EXECUTE on SelectAllStructureLocationLinks to public
	 

	  INSERT INTO DBVersion values (53, 
		   N'Grant permissions on spatial user-defined-functions',getDate(),User_ID())

	 COMMIT TRANSACTION fiftythree
	end

	
	if(not(exists(select (1) from DBVersion where DBVersionID = 54)))
	begin
     print N'Create ufn for measuring structure area'
     BEGIN TRANSACTION fiftyfour

	 EXEC('
	 CREATE FUNCTION ufnStructureArea
	(
		-- Add the parameters for the function here
		@StructureID bigint
	)
	RETURNS float
	AS
	BEGIN
		declare @Area float
		declare @AreaScalar float
		--Measures the area of the PSD
		set @AreaScalar = dbo.XYScale() * dbo.ZScale()

	
		select top 1 @Area = sum(MosaicShape.STLength()) * @AreaScalar from Location 
		where ParentID = @StructureID
		group by ParentID
	  
		-- Return the result of the function
		RETURN @Area

	END
	')

	Grant EXECUTE on ufnStructureArea to public
	 

	  INSERT INTO DBVersion values (54, 
		   N'Create ufn for measuring structure area',getDate(),User_ID())

	 COMMIT TRANSACTION fiftyfour
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 55)))
	begin
     print N'Create stored procedure for recursively selecting child IDs for a structure'
     BEGIN TRANSACTION fiftyfive

	 EXEC('
		CREATE PROCEDURE [dbo].[RecursiveSelectChildStructureIDs]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY
			AS
			BEGIN 	 
				DECLARE @NumSeedStructures int
				DECLARE @SeedStructures integer_list
				DECLARE @ChildStructures integer_list 

				insert into @SeedStructures select ID from @IDs 

				select @NumSeedStructures=count(ID) from @SeedStructures

				while @NumSeedStructures > 0
				BEGIN
					DECLARE @NewChildStructures integer_list 
					insert into @NewChildStructures
						select distinct Child.ID from Structure Child
							inner join @SeedStructures Parents on Parents.ID = Child.ParentID

					delete from @SeedStructures
					insert into @SeedStructures select ID from @NewChildStructures
					select @NumSeedStructures=count(ID) from @SeedStructures

					insert into @ChildStructures select ID from @NewChildStructures
					delete from @NewChildStructures
				END

				select ID from @ChildStructures
			END
	')

	Grant EXECUTE on RecursiveSelectChildStructureIDs to public
	  
	  INSERT INTO DBVersion values (55, 
		   N'Create stored procedure for recursively selecting child IDs for a structure',getDate(),User_ID())

	 COMMIT TRANSACTION fiftyfive
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 56)))
	begin
     print N'Create ufn for measuring structure volume'
     BEGIN TRANSACTION fiftysix

	 EXEC('
	 CREATE FUNCTION ufnStructureVolume
	(
		-- Add the parameters for the function here
		@StructureID bigint
	)
	RETURNS float
	AS
	BEGIN
		declare @Area float
		declare @AreaScalar float
		--Measures the area of the PSD
		set @AreaScalar = dbo.XYScale() * dbo.ZScale()

		select top 1 @Volume = sum(MosaicShape.STArea()) * @AreaScalar from Location 
		where ParentID = @StructureID
		group by ParentID
	  
		-- Return the result of the function
		RETURN @Volume

	END
	')

	Grant EXECUTE on ufnStructureVolume to public
	 

	  INSERT INTO DBVersion values (56, 
		   N'Create ufn for measuring structure volume',getDate(),User_ID())

	 COMMIT TRANSACTION fiftysix

	 

	if(not(exists(select (1) from DBVersion where DBVersionID = 57)))
	begin
     print N'Update UDTs for Locations to include width column'
     BEGIN TRANSACTION fiftyseven

		EXEC('
		ALTER FUNCTION [dbo].[SectionLocations](@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location where Z = @Z
				);
		')
		
		EXEC('
			ALTER FUNCTION [dbo].[SectionLocationsModifiedAfterDate](@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location 
					where Z = @Z AND LastModified >= @QueryDate
				);
		')

	  INSERT INTO DBVersion values (57, 
		    N'Update UDTs for Locations to include width column',getDate(),User_ID())

	 COMMIT TRANSACTION fiftyseven
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 58)))
	begin
     print N'Add unique constraint to PermittedStructureLink table'
     BEGIN TRANSACTION fiftyeight
	  
	 ALTER TABLE [dbo].[PermittedStructureLink] ADD  CONSTRAINT [PermittedStructureLink_source_target_unique] UNIQUE NONCLUSTERED 
	 (
		[SourceTypeID] ASC,
		[TargetTypeID] ASC
	 )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
	 
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	 INSERT INTO DBVersion values (58, 
		    N'Add unique constraint to PermittedStructureLink table',getDate(),User_ID())

	 COMMIT TRANSACTION fiftyeight
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 59)))
	begin
     print N'Fixed bug in StructureLocationLinks where a specific database was named'
     BEGIN TRANSACTION fiftynine
	  
	  EXEC('
			ALTER FUNCTION [dbo].[StructureLocationLinks](@StructureID bigint)
				RETURNS TABLE 
				AS
				RETURN(
 						select LLA.* from  LocationLink LLA 
						inner join Location L ON LLA.A = L.ID
						where L.ParentID = @StructureID
						union
						select LLB.* from LocationLink LLB  
						inner join Location L ON LLB.B = L.ID
						where L.ParentID = @StructureID
						)
						')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		INSERT INTO DBVersion values (59, 
		    N'Fixed bug in StructureLocationLinks where a specific database was named' ,getDate(),User_ID())

	 COMMIT TRANSACTION fiftynine
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 60)))
	begin
     print N'Added Stored Procedure to return NetworkChildStructures'
     BEGIN TRANSACTION sixty
	  
	  EXEC('
			
			CREATE PROCEDURE [dbo].[SelectNetworkChildStructures]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				DECLARE @CellsInNetwork integer_list 
				DECLARE @ChildrenInNetwork integer_list 

				insert into @CellsInNetwork exec SelectNetworkStructureIDs @IDs, @Hops

				select S.* from Structure S 
					inner join @CellsInNetwork N ON N.ID = S.ParentID
			END
			
						')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		INSERT INTO DBVersion values (60, 
		     N'Added Stored Procedure to return NetworkChildStructures' ,getDate(),User_ID())

	 COMMIT TRANSACTION sixty
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 61)))
	begin
     print N'Fixed bugs in Network stored procedures'
     BEGIN TRANSACTION sixtyone
	  
	  /*Update the return value to use "ID" for the column name instead of "SourceID"*/
	  EXEC('DROP PROCEDURE SelectNetworkChildStructureIDs')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	  EXEC('DROP PROCEDURE SelectNetworkStructureIDs')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 /*Update the return value to use "ID" for the column name instead of "SourceID"*/
	  EXEC('CREATE FUNCTION NetworkStructureIDs
			(
				-- Add the parameters for the function here
				@IDs integer_list READONLY,
				@Hops int
			)
			RETURNS @CellsInNetwork TABLE 
			(
				-- Add the column definitions for the TABLE variable here
				ID bigint PRIMARY KEY
			)
			AS
			BEGIN
				-- Fill the table variable with the rows for your result set
	
				DECLARE @HopSeedCells integer_list 

				insert into @HopSeedCells select ID from @IDs 
				insert into @CellsInNetwork select ID from @IDs 

				while @Hops > 0
				BEGIN
					DECLARE @HopSeedCellsChildStructures integer_list
					DECLARE @ChildStructurePartners integer_list
					DECLARE @HopCellsFound integer_list
		
					insert into @HopSeedCellsChildStructures
						select distinct Child.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @HopSeedCells Cells ON Cells.ID = Parent.ID
		
					insert into @ChildStructurePartners
						select distinct SL.TargetID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.SourceID
						UNION
						select distinct SL.SourceID from StructureLink SL
							inner join @HopSeedCellsChildStructures C ON C.ID = SL.TargetID
				 
					insert into @HopCellsFound 
						select distinct Parent.ID from Structure Parent
							inner join Structure Child ON Child.ParentID = Parent.ID
							inner join @ChildStructurePartners Partners ON Partners.ID = Child.ID
						where Parent.ID not in (Select ID from @CellsInNetwork union select ID from @HopSeedCells)
		
					delete S from @HopSeedCells S
		
					insert into @HopSeedCells 
						select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)

					insert into @CellsInNetwork select ID from @HopCellsFound 
						where ID not in (Select ID from @CellsInNetwork)
			 

					delete from @ChildStructurePartners
					delete from @HopCellsFound
			 
					set @Hops = @Hops - 1
				END 

				RETURN 
			END')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
		    CREATE FUNCTION [dbo].[NetworkChildStructureIDs]
			(
				-- Add the parameters for the function here
				@IDs integer_list READONLY,
				@Hops int
			)
			RETURNS @ChildStructuresInNetwork TABLE 
			(
				-- Add the column definitions for the TABLE variable here
				ID bigint PRIMARY KEY
			)
			AS
			BEGIN
				-- Fill the table variable with the rows for your result set
				DECLARE @ChildIDsInNetwork integer_list 
	 
				insert into @ChildIDsInNetwork 
					select ChildStruct.ID from Structure S
					inner join NetworkStructureIDs(@IDs, @Hops) N ON S.ID = N.ID
					inner join Structure ChildStruct ON ChildStruct.ParentID = N.ID

				insert into @ChildStructuresInNetwork 
					select SL.SourceID as ID from StructureLink SL
						where SL.SourceID in (Select ID from @ChildIDsInNetwork)
					UNION
					select SL.TargetID as ID from StructureLink SL
						where SL.TargetID in (Select ID from @ChildIDsInNetwork)

				RETURN
			END')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
		    ALTER PROCEDURE [dbo].[SelectNetworkStructures]
				-- Add the parameters for the stored procedure here
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				select S.* from Structure S 
					inner join NetworkStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END')
			
		  if(@@error <> 0)
			 begin
			   ROLLBACK TRANSACTION 
			   RETURN
			 end

			 if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		 EXEC('
		    ALTER PROCEDURE [dbo].[SelectNetworkChildStructures]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				select S.* from Structure S 
					inner join NetworkChildStructureIDs(@IDs, @Hops) N ON N.ID = S.ID
			END')
			
		  if(@@error <> 0)
			 begin
			   ROLLBACK TRANSACTION 
			   RETURN
			 end

		  EXEC('
			ALTER PROCEDURE [dbo].[SelectNetworkStructureLinks]
						-- Add the parameters for the stored procedure here
						@IDs integer_list READONLY,
						@Hops int
			AS
			BEGIN
				select SL.* from StructureLink SL
					where SL.SourceID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops)) OR
							SL.TargetID in (Select ID from NetworkChildStructureIDs( @IDs, @Hops))
			END')
			
		  if(@@error <> 0)
			 begin
			   ROLLBACK TRANSACTION 
			   RETURN
			 end

		

		INSERT INTO DBVersion values (61, 
		     N'Fixed bugs in Network stored procedures' ,getDate(),User_ID() )
		
	 COMMIT TRANSACTION sixtyone
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 62)))
	begin
     print N'Updated SectionLocations function to ensure they use the new width column'
     BEGIN TRANSACTION sixtytwo
	  
	  EXEC('
			ALTER FUNCTION [dbo].[SectionLocations](@Z float)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location where Z = @Z
				);
						')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	  EXEC('
			ALTER FUNCTION [dbo].[SectionLocationsModifiedAfterDate](@Z float, @QueryDate datetime)
			RETURNS TABLE 
			AS
			RETURN(
 					Select * from Location 
					where Z = @Z AND LastModified >= @QueryDate
				);
						')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		INSERT INTO DBVersion values (62, 
		    N'Fixed bug in StructureLocationLinks where a specific database was named' ,getDate(),User_ID())

	 COMMIT TRANSACTION sixtytwo
	end

	if(not(exists(select (1) from DBVersion where DBVersionID = 63)))
	begin
     print N'Re-add the select NetworkStructureID and NetworkChildStructureID procedures to work around entity framework issues with udt parameters'
     BEGIN TRANSACTION sixtythree
	  
	  EXEC('CREATE PROCEDURE [dbo].SelectNetworkStructureIDs
				-- Add the parameters for the stored procedure here
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				select N.ID as ID from NetworkStructureIDs(@IDs, @Hops) N
			END')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

	  EXEC('CREATE PROCEDURE [dbo].SelectNetworkChildStructureIDs
				-- Add the parameters for the stored procedure here
				@IDs integer_list READONLY,
				@Hops int
			AS
			BEGIN
				select N.ID as ID from NetworkChildStructureIDs(@IDs, @Hops) N
			END')
			
	  if(@@error <> 0)
		 begin
		   ROLLBACK TRANSACTION 
		   RETURN
		 end

		INSERT INTO DBVersion values (63, 
		    N'Re-add the select NetworkStructureID and NetworkChildStructureID procedures to work around entity framework issues with udt parameters' ,getDate(),User_ID())

	 COMMIT TRANSACTION sixtythree
	end

	 
--from here on, continually add steps in the previous manner as needed.
	COMMIT TRANSACTION main