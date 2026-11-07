-- MergeStructures: merges structure @MergeStructureID into @KeepStructureID. Moves its locations,
-- child structures and structure links to the keep structure (links between the two are deleted),
-- appends its Notes to the keep structure's Notes between BEGIN/END MERGE FROM markers (a NULL keep
-- Notes counts as empty), then deletes the merged structure.
-- Refuses, with error 50000 and nothing changed, when the keep structure is the merged structure or lies
-- below it: moving the merged structure's children to the keep structure would make the keep structure its
-- own parent or close a cycle of parents.
-- Callers: gRPC StructureService.Merge (AnnotationContextProcedures.MergeStructuresAsync), WCF
-- AnnotateService.Merge (ConnectomeDataModel).
 CREATE PROCEDURE [dbo].[MergeStructures]
					-- Add the parameters for the stored procedure here
					@KeepStructureID bigint,
					@MergeStructureID bigint
				AS
				BEGIN
					-- SET NOCOUNT ON added to prevent extra result sets from
					-- interfering with SELECT statements.
					SET NOCOUNT ON;

					-- The visited set ends the walk at a cycle already stored that does not pass through the merged structure.
					declare @Ancestor bigint = @KeepStructureID;
					declare @Visited table (ID bigint PRIMARY KEY);
					declare @Refusal nvarchar(400);
					while @Ancestor IS NOT NULL
					BEGIN
						IF @Ancestor = @MergeStructureID
						BEGIN
							set @Refusal = CONCAT(N'MergeStructures: cannot merge structure ', @MergeStructureID,
								N' into structure ', @KeepStructureID, N', which is the same structure or one of its descendants');
							THROW 50000, @Refusal, 1;
						END

						insert into @Visited (ID) values (@Ancestor);
						set @Ancestor = (select S.ParentID from Structure S
										 where S.ID = @Ancestor
										   and not exists (select 1 from @Visited V where V.ID = S.ParentID));
					END

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
						set Notes = ISNULL(Notes, N'') + @crlf + @MergeHeader + @crlf + @MergeNotes + @crlf + @MergeFooter + @crlf
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
