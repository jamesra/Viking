using System;
using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using Geometry;

namespace LocalBookmarks
{
    static class MigrateV1ToV2
    {
        public static connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd.XRoot Migrate(string BookmarkPath)
        {
            connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot oldRoot = connectomes.utah.edu.XSD.BookmarkSchema.xsd.XRoot.Load(BookmarkPath);

            if (!TryCreatingMigrationBackup(BookmarkPath))
                return null;

            XRoot newRoot = new XRoot(MigrateFolder(oldRoot.Folder));
            newRoot.Folder.Shape = ShapeType.STAR.ToShapeString();

            return newRoot; 
        }

        private static Folder MigrateFolder(connectomes.utah.edu.XSD.BookmarkSchema.xsd.Folder oldFolder)
        {
            Folder newFolder = new Folder();
            newFolder.Name = oldFolder.name;
            newFolder.Shape = ShapeType.INHERIT.ToShapeString();

            foreach(var oldBookmark in oldFolder.Bookmarks)
            {
                var newBookmark = MigrateBookmark(oldBookmark);
                if (newBookmark != null)
                {
                    newFolder.Bookmarks.Add(newBookmark);
                }
            }

            foreach(var oldSubFolder in oldFolder.Folders)
            {
                var newSubFolder = MigrateFolder(oldSubFolder);
                if(newSubFolder != null)
                {
                    newFolder.Folders.Add(newSubFolder);
                }
            }

            return newFolder;
        }

        private static Bookmark MigrateBookmark(connectomes.utah.edu.XSD.BookmarkSchema.xsd.Bookmark oldBookmark)
        {
            Bookmark newBookmark = new Bookmark();
            newBookmark.Name = oldBookmark.name;
            newBookmark.Z = oldBookmark.Position.Z;
            newBookmark.VolumePosition = new Point2D(oldBookmark.Position.X, oldBookmark.Position.Y);
            newBookmark.View = new View();
            newBookmark.View.Downsample = oldBookmark.View.Downsample;
            newBookmark.Comment = oldBookmark.Comment; 

            Viking.VolumeModel.IVolumeToSectionTransform transform = Viking.UI.State.volume.GetSectionToVolumeTransform((int)newBookmark.Z);

            GridVector2 MosaicPosition; 
            if(transform.TryVolumeToSection(oldBookmark.Position.ToGridVector2(), out MosaicPosition))
            {
                newBookmark.MosaicPosition = new connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd.Point2D(MosaicPosition);
            }

            return newBookmark;
        }

        private static bool TryCreatingMigrationBackup(string BookmarkPath)
        {
            string baseDir = System.IO.Path.GetDirectoryName(BookmarkPath);
            string filename = System.IO.Path.GetFileName(BookmarkPath);

            int BackupNumber = 0;
            string migratedFilename = "PreMigration" + BackupNumber + "_" +  filename;
            string migratedFullPath = System.IO.Path.Combine(baseDir, migratedFilename);

            while(System.IO.File.Exists(migratedFullPath))
            {
                BackupNumber++;
                migratedFilename = "PreMigration" + BackupNumber + "_" + filename;
                migratedFullPath = System.IO.Path.Combine(baseDir, migratedFilename);
            }

            try
            {
                System.IO.File.Move(BookmarkPath, migratedFullPath);
            }
            catch (Exception)
            {
                return false;
            }

            return true; 
        }
    }
}
