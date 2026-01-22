using System.Xml.Linq;

namespace LocalBookmarks
{
    class HTMLExporter(FolderUIObj root)
    {
        /// <summary>
        /// The top level folder to export
        /// </summary>
        public FolderUIObj ExportRoot = root;

        public XDocument Document = new();

        public void WriteHTML(string Filename)
        {
            Document = new XDocument();
            XElement HTMLElement = WriteHeader();
            WriteBody(HTMLElement);

            Document.Save(Filename);
        }

        private XElement WriteHeader()
        {
            XDocumentType docType = new("HTML", "-//W3C//DTD HTML 4.0 Transitional//EN", "", "");
            Document.Add(docType);

            XElement Title = new("title");
            Title.SetValue(ExportRoot.Name);
            XElement HeadElement = new("head", Title);
            XElement HtmlElement = new("html", HeadElement);

            Document.Add(HtmlElement);

            return HtmlElement;
        }

        private void WriteBody(XElement HTMLElement)
        {
            XElement BodyElement = new("body");

            //Include some instructions
            XElement introParagraph = new("p");
            XElement partOne = new("p");
            partOne.SetValue("This document is intended to be used in conjunction with Viking.  If you have not installed Viking before you should run the ");
            XElement vikingAnchor = new("a");
            vikingAnchor.SetAttributeValue("href", "http://connectomes.utah.edu/");
            vikingAnchor.SetValue("installer");
            XElement partTwo = new("p");
            partTwo.SetValue(" to ensure all prerequisites are installed.");

            introParagraph.Add(partOne);
            introParagraph.Add(vikingAnchor);
            introParagraph.Add(partTwo);

            BodyElement.Add(introParagraph);

            ExportFolder(BodyElement, ExportRoot);

            HTMLElement.Add(BodyElement);
        }

        private void ExportFolder(XElement element, FolderUIObj parent)
        {
            XElement h4 = new("h4");
            h4.SetValue(parent.Name);

            element.Add(h4);

            XElement ul = new("ul");
            foreach (BookmarkUIObj bookmark in parent.Bookmarks)
            {
                XElement li = new("li");
                ExportBookmark(li, bookmark);
                ul.Add(li);
            }

            foreach (FolderUIObj folder in parent.Folders)
            {
                XElement li = new("li");
                ExportFolder(li, folder);
                ul.Add(li);
            }

            element.Add(ul);
        }

        private void ExportBookmark(XElement element, BookmarkUIObj bookmark)
        {
            XElement bold = new("b");
            XElement anchor = new("a");

            anchor.SetAttributeValue("href", bookmark.URI);
            anchor.SetAttributeValue("target", "Viking");
            anchor.SetValue(bookmark.Name);

            bold.Add(anchor);
            element.Add(bold);

            //Add the cut & paste coordinates
            XElement paragraph = new("p");
            XElement Coords = new("i");
            Coords.SetValue(bookmark.CutPasteCoords);
            paragraph.Add(Coords);
            if (bookmark.Comment != null)
            {
                XElement commentParagraph = new("p");
                commentParagraph.SetValue(bookmark.Comment);
                paragraph.Add(commentParagraph);
            }

            element.Add(paragraph);
        }
    }
}
