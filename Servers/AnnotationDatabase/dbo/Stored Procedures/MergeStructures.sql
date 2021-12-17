 CREATE PROCEDURE [dbo].[MergeStructures]
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

					IF NOT (@MergeNotes IS NULL OR @MergeNotes = '')
					BEGIN
						declare @crlf nvarchar(2)
						set @crlf = CHAR(13) + CHAR(10)

						declare @MergeHeader nvarchar(80)
						declare @MergeFooter nvarchar(80)
						set @MergeHeader = '*****BEGIN MERGE FROM ' + CONVERT(nvarchar(80), @MergeStructureID) + '*****'
						set @MergeFooter = '*****END MERGE FROM ' + CONVERT(nvarchar(80), @MergeStructureID) + '*****'

						update Structure
						set Notes = Notes + @crlf + @MergeHeader + @crlf + @MergeNotes + @crlf + @MergeFooter + @crlf
						where ID = @KeepStructureID
					END

					-- Delete any structure links directly between the keep and merge structures, a rare occurrence from incorrect annotations
					delete StructureLink where SourceID = @KeepStructureID AND TargetID = @MergeStructureID
					delete StructureLink where TargetID = @KeepStructureID AND SourceID = @MergeStructureID

					update StructureLink
					set TargetID = @KeepStructureID
					where TargetID = @MergeStructureID
		
					update StructureLink
					set SourceID = @KeepStructureID
					where SourceID = @MergeStructureID

					update Structure
					set Notes = 'Merged into structure ' + CONVERT(nvarchar(80), @KeepStructureID)
					where ID = @MergeStructureID

					delete Structure
					where ID = @MergeStructureID
				END   