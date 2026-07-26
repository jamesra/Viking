
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
		 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ApproximateStructureLocations] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[ApproximateStructureLocations] TO [AnnotationPowerUser]
    AS [dbo];

