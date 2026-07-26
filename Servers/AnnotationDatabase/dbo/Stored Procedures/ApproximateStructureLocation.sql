
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

		 END
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ApproximateStructureLocation] TO [AnnotationPowerUser]
    AS [dbo];


GO
GRANT VIEW DEFINITION
    ON OBJECT::[dbo].[ApproximateStructureLocation] TO [AnnotationPowerUser]
    AS [dbo];

