
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
			