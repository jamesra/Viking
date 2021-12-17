
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
		END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[SelectLastModifiedLocationByUsers] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[SelectLastModifiedLocationByUsers] TO [AnnotationPowerUser]
    AS [dbo];

