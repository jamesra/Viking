using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.Common;
using connectomes.utah.edu.XSD.BookmarkSchemaV2.xsd;
using LocalBookmarks;
using Geometry;
using Viking.UI.Controls;
using Viking.Common.UI;

namespace LocalBookmarks
{
    [TreeViewVisible()]
    partial class BookmarkUIObj : UIObjTemplate<Bookmark>
    {
        internal static float LabelScaleFactor = 2.25f; 

        public BookmarkUIObj(FolderUIObj parent)
        {
            Data = new Bookmark();
            Parent = parent;
            this.CallOnCreate();
        }

        public BookmarkUIObj(FolderUIObj parent, Bookmark bookmark)
        {
            Data = bookmark;
            _Parent = parent;
        }

        protected static event EventHandler OnCreate;
        protected void CallOnCreate()
        {
            if (OnCreate != null)
            {
                Viking.UI.State.MainThreadDispatcher.BeginInvoke(OnCreate, new object[] { this, null });
            }
        }
        public static event EventHandler Create
        {
            add { OnCreate += value; }
            remove { OnCreate -= value; }
        }



        public override string Name
        {
            get { return Data.Name; }
            set
            {
                Data.Name = value;
                if (Data.Name == null)
                    Data.Name = ""; 

                _LabelSizeMeasured = false; 
                ValueChangedEvent("Name"); 
            }
        }

        public Point2D Position
        {
            get { if(Data.VolumePosition == null)
                    Data.VolumePosition = new Point2D();
                  return Data.VolumePosition;
            }
            set
            {
                Data.VolumePosition = value; 
            }
        }

        public Point2D MosaicPosition
        {
            get
            {
                return Data.MosaicPosition;
            }
            set
            {
                Data.MosaicPosition = value;
            }
        }

        public View View
        {
            get
            {
                if (Data.View == null)
                    Data.View = new View();
                return Data.View;
            }
            set
            {
                Data.View = value;
            }
        }

        public GridVector2 GridPosition
        {
            get { return new GridVector2(X, Y); }
        }

        public double X
        {
            get { return System.Convert.ToDouble(Data.VolumePosition.X); }
            set
            {
                Position.X = (float)value;
                  ValueChangedEvent("X");
            }
        }

        public double Y
        {
            get { return System.Convert.ToDouble(Data.VolumePosition.Y); }
            set
            {
                Position.Y = (float)value;
                ValueChangedEvent("Y");
            }
        }

        public int Z
        {
            get { return System.Convert.ToInt32(Math.Round(Data.Z)); }
            set
            {
                Data.Z = (float)value;
                ValueChangedEvent("Z");
            }
        }

        public string Comment
        {
            get { return Data.Comment; }
            set
            {
                Data.Comment = value;
                ValueChangedEvent("Comment");
            }
        }

        public double Downsample
        {
            get { return View.Downsample; }
            set
            {
                View.Downsample = value;
                ValueChangedEvent("Downsample");
            }
        }

        #region Export

        public string URI
        {
            get
            {
                return Viking.Common.Util.CoordinatesToURI(X, Y, Z, Downsample);
            }
        }

        public string HTMLAnchor
        {
            get
            {
                string html = "<b><A HREF=\"" + URI + "\" " +
                              ">" + Name + "<//A><//b><br/>" +
                              Viking.Common.Util.CoordinatesToCopyPaste(X,Y,Z,Downsample) + 
                              " <br/>" +
                              "<p>" + Comment + "</p>";  
                return html;
            }
        }

        public string CutPasteCoords
        {
            get
            {
                return Viking.Common.Util.CoordinatesToCopyPaste(X,Y,Z,Downsample);
            }
        }

        #endregion

        #region IUIObject

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            BookmarkTreeNode node = new BookmarkTreeNode(this);
            node.Name = this.Name;
            return node;
        }

        public override int TreeImageIndex
        {
            get
            {
                return 2; 
            }
        }

        public override int  TreeSelectedImageIndex
        {
	        get 
    	    {
                return 2; 
        	}
        }

        public override void Delete()
        {
            CallBeforeDelete(); 
            Parent.RemoveChild(this); 
          //  Parent.Data.Bookmarks.Remove(this.Data);
            CallAfterDelete();
            Global.Save();
        }

        public override string ToolTip
        {
            get
            {
                return this.Comment;  
            }
        }

        #endregion

        bool _LabelSizeMeasured = false; 
        Microsoft.Xna.Framework.Vector2 _LabelSize;
        public Microsoft.Xna.Framework.Vector2 GetLabelSize(Microsoft.Xna.Framework.Graphics.SpriteFont font)
        {
            if (_LabelSizeMeasured)
                return _LabelSize;

            string label = this.ToString();
            //Label can't be empty or the offset measured is zero
            if (label == "")
                label = " ";

            _LabelSize = font.MeasureString(this.ToString());
            _LabelSizeMeasured = true;
            return _LabelSize;
        }
    }
}
