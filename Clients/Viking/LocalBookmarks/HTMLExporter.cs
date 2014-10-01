using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq; 
using System.IO;

namespace LocalBookmarks
{
    class HTMLExporter
    {
        /// <summary>
        /// The top level folder to export
        /// </summary>
        public FolderUIObj ExportRoot;

        public XDocument Document; 

        public HTMLExporter(FolderUIObj root)
        {
            Document = new XDocument();
            ExportRoot = root; 
        }

        public void WriteHTML(string Filename)
        {
            Document = new XDocument();
            XElement HTMLElement = WriteHeader();
            WriteBody(HTMLElement);

            Document.Save(Filename);
        }

        private XElement WriteHeader()
        {
            XDocumentType docType = new XDocumentType("HTML", "-//W3C//DTD HTML 4.0 Transitional//EN", "", "");
            Document.Add(docType);
            
            XElement Title = new XElement("title");
            Title.SetValue(ExportRoot.Name);
            XElement HeadElement = new XElement("head", Title);
            XElement HtmlElement = new XElement("html", HeadElement);

            Document.Add(HtmlElement);

            return HtmlElement; 
        }

        private void WriteBody(XElement HTMLElement)
        {
            XElement BodyElement = new XElement("body");

            //Include some instructions
            XElement introParagraph = new XElement("p");
            XElement partOne = new XElement("p");
            partOne.SetValue("This document is intended to be used in conjunction with Viking.  If you have not installed Viking before you should run the ");
            XElement vikingAnchor = new XElement("a");
            vikingAnchor.SetAttributeValue("href", "http://connectomes.utah.edu/");
            vikingAnchor.SetValue("installer");
            XElement partTwo = new XElement("p");
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
            XElement h4 = new XElement("h4");
            h4.SetValue(parent.Name);

            element.Add(h4);

            XElement ul = new XElement("ul");
            foreach(BookmarkUIObj bookmark in parent.Bookmarks)
            {
                XElement li = new XElement("li");
                ExportBookmark(li, bookmark);
                ul.Add(li);
            }

            foreach (FolderUIObj folder in parent.Folders)
            {
                XElement li = new XElement("li");
                ExportFolder(li, folder);
                ul.Add(li);
            }

            element.Add(ul);
        }

        private void ExportBookmark(XElement element, BookmarkUIObj bookmark)
        {
            XElement bold = new XElement("b");
            XElement anchor = new XElement("a");

            anchor.SetAttributeValue("href", bookmark.URI);
            anchor.SetAttributeValue("target", "Viking"); 
            anchor.SetValue(bookmark.Name);

            bold.Add(anchor);
            element.Add(bold); 

            //Add the cut & paste coordinates
            XElement paragraph = new XElement("p");
            XElement Coords = new XElement("i");
            Coords.SetValue(bookmark.CutPasteCoords);
            paragraph.Add(Coords);
            if (bookmark.Comment != null)
            {
                XElement commentParagraph = new XElement("p");
                commentParagraph.SetValue(bookmark.Comment);
                paragraph.Add(commentParagraph); 
            }

            element.Add(paragraph); 
        }
    }
}
