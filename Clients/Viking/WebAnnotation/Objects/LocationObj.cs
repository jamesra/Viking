using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; 

using WebAnnotation.Service;
using System.Drawing;
using System.Windows.Forms;

using Geometry; 

using Common.UI;
using Viking.Common; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation.UI; 

namespace WebAnnotation.Objects
{
    public enum LocationType
    {
        POINT = 0,
        CIRCLE = 1,
        MASK = 2
    };

    
    public class LocationObj : WCFObjBaseWithKey<Location>
    {
        public string Label
        {
            get
            {
                if (Parent == null)
                    return "";

                if (Parent.Type == null)
                    return "";

                return Parent.Type.Code + " " + Parent.ID.ToString();
            }
        }

        /// <summary>
        /// Return true if the passed point falls within our location
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public bool Contains(WebAnnotation.ViewModel.SectionAnnotationViewModel sectionAnnotations, GridVector2 pos)
        {
            GridVector2 locPosition = sectionAnnotations.GetPositionForLocation(this.ID);
            switch(this.TypeCode)
            {
                case LocationType.POINT: //A point
                    //HACK, assume label size is 256 pixels across
                    Vector2 offset = GetLabelSize();
                    offset.X *= AnnotationOverlay.LocationTextScaleFactor;
                    offset.Y *= AnnotationOverlay.LocationTextScaleFactor;

                    GridRectangle  _Bounds = new GridRectangle(new GridVector2(locPosition.X - (offset.X / 2), locPosition.Y - (offset.Y / 2)),
                                                        offset.X,
                                                        offset.Y);
                    return _Bounds.Contains(pos);
                case LocationType.CIRCLE: //A circle
                    double distance = GridVector2.Distance(locPosition, pos);
                    return distance <= Radius;
                        
                default:
                    Trace.WriteLine("Calling LocationObj::Contains on an unknown type of location", "WebAnnotation"); 
                    return false; 
            }

        }

        public long? ParentID
        {
            get { return Data.ParentID; }
        }

        private StructureObj _Parent;
        public StructureObj Parent
        {
            get
            {
                if (_Parent != null)
                    return _Parent;

                if (ParentID.HasValue == false)
                    return null;

                _Parent = Store.Structures.GetObjectByID(ParentID.Value);
                return _Parent;
            }
        }

        public double X
        {
            get { return Data.Position.X; }
            set {
                AnnotationPoint point = new AnnotationPoint();
                point.X = value;
                point.Y = Data.Position.Y;
                point.Z = Data.Position.Z;
                Data.Position = point;
                SetDBActionForChange();
            }
        }

        public double Y 
        {
            get { return Data.Position.Y; }
            set
            {
                AnnotationPoint point = new AnnotationPoint();
                point.X = Data.Position.X;
                point.Y = value;
                point.Z = Data.Position.Z;
                Data.Position = point;
                SetDBActionForChange();
            }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        [ColumnAttribute("Z")]
        public double Z
        {
            get {return Data.Position.Z; }
        }

        

        /// <summary>
        /// VolumeX is the x position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        [ColumnAttribute("Volume X")]
        public double VolumeX
        {
            get
            {
                return Data.VolumePosition.X; 
            }
            set
            {
                if (value != Data.VolumePosition.X)
                {
                    AnnotationPoint point = new AnnotationPoint();
                    point.X = value;
                    point.Y = Data.VolumePosition.Y;
                    point.Z = Data.VolumePosition.Z;
                    Data.VolumePosition = point;
                    SetDBActionForChange();
                }
            }
        }

        /// <summary>
        /// VolumeY is the y position in volume space. It only exists to inform the database of an estimate of the locations position in volume space.
        /// We want the database to have this value so data processing tools don't need to implement the transforms
        /// It should not be used by the viewer since the viewer can calculate the value.*/
        /// </summary>
        [ColumnAttribute("Volume Y")]
        public double VolumeY
        {
            get
            {
                return Data.VolumePosition.Y;
            }
            set
            {
                if (value != Data.VolumePosition.Y)
                {
                    AnnotationPoint point = new AnnotationPoint();
                    point.X = Data.VolumePosition.X;
                    point.Y = value;
                    point.Z = Data.VolumePosition.Z;
                    Data.VolumePosition = point;
                    SetDBActionForChange();
                }
            }
        }

        [ColumnAttribute("Radius")]
        public double Radius
        {
            get { return Data.Radius; }
            set { Data.Radius = value;
            SetDBActionForChange();
            }
        }

        [ColumnAttribute("Type")]
        public LocationType TypeCode
        {
            get { return (LocationType)Data.TypeCode; }
            set { Data.TypeCode = (short)value;
            SetDBActionForChange();
            }
        }

        /// <summary>
        /// This column is set to true when the location has one link and is not marked as terminal.  It means the
        /// Location is a dead-end and the user did not mark it as a dead end, which means it may actually continue
        /// and the user was distracted
        /// </summary>
        [ColumnAttribute("Unverified Terminal")]
        public bool IsUnverifiedTerminal
        {
            get
            {
                if (Links.Length >= 2)
                    return false; 
                return !(Terminal || OffEdge);
            }
        }

        /// <summary>
        /// This is readonly because changing it would break a datastructure in location store
        /// and also would require update of X,Y to the section space of the different section
        /// </summary>
        public long Section
        {
            get { return Data.Section; }
        }

        public AnnotationPoint Position
        {
            get { return Data.Position; }
            set { Data.Position = value;
            SetDBActionForChange();
            }
        }

        public long[] Links
        {
            get {
                if (Data.Links == null)
                {
                    return new long[0];
                }
                return Data.Links; 
            }
        }

        /// <summary>
        /// Adjust the client after a link is created
        /// </summary>
        /// <param name="ID"></param>
        public void AddLink(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't add own ID from location links");

            List<long> listLinks = Data.Links.ToList<long>(); 
            listLinks.Add(ID); 
            Data.Links = listLinks.ToArray(); 
        }

        /// <summary>
        /// Adjust the client after a link is removed
        /// </summary>
        /// <param name="ID"></param>
        public void RemoveLink(long ID)
        {
            if (ID == this.ID)
                throw new ArgumentException("Can't remove own ID from location links");

            List<long> listLinks = Data.Links.ToList<long>();
            listLinks.Remove(ID);
            Data.Links = listLinks.ToArray();
        }

        public bool Terminal
        {
            get { return Data.Terminal; }
            set { Data.Terminal = value; }
        }

        public bool OffEdge
        {
            get { return Data.OffEdge; }
            set { Data.OffEdge = value; }
        }

        public DateTime LastModified
        {
            get { return new DateTime(Data.LastModified, DateTimeKind.Utc); }
        }
        
        /// <summary>
        /// The radius to use when the location is displayed as a reference location on another section
        /// </summary>
        public float OffSectionRadius
        {
            get
            {
                return (float)(this.Radius / 4 > 128f ? 128f : this.Radius / 4);
            }
        }

        public LocationObj()
        {
        }

        public LocationObj(Location obj)
        {
            Data = obj; 
        }

        public LocationObj(StructureObj parent, GridVector2 position, GridVector2 volumePosition, int SectionNumber)
        {
            this.Data = new Location();
            this.Data.DBAction = DBACTION.INSERT;
            this.Data.ID = Store.Locations.GetTempID();
            this.Data.Tags = new String[0];
            this.Data.Verticies = new AnnotationPoint[0];
            this.Data.TypeCode = 1;
            this.Data.Radius = 16;
            this.Data.Links = new long[0];

            AnnotationPoint P = new AnnotationPoint();
            P.X = position.X;
            P.Y = position.Y;
            P.Z = (double)SectionNumber;

            AnnotationPoint VP = new AnnotationPoint();
            VP.X = volumePosition.X;
            VP.Y = volumePosition.Y;
            VP.Z = (double)SectionNumber;

            this.Data.Section = SectionNumber; 

            this.Data.Position = P;
            this.Data.VolumePosition = VP; 

            if (parent != null)
            {
                this.Data.ParentID = parent.ID;
            }

            Store.Locations.Add(this);

            CallOnCreate(); 
        }     
        
        #region Render Code

        
        private bool _LabelSizeMeasured = false;
        private Vector2 _LabelSize;

        private bool _InfoLabelSizeMeasured = false;
        private Vector2 _InfoLabelSize;

        private bool _ParentLabelSizeMeasured = false;
        private Vector2 _ParentLabelSize;

        
        public Vector2 GetLabelSize()
        {
            if (_LabelSizeMeasured)
                return _LabelSize;

            //Otherwise we aren't really sure about the label size, so just guess
            return new Vector2(256f, 128f); 
        }

        public Vector2 GetLabelSize(SpriteFont font)
        {
            if(_LabelSizeMeasured)
                return _LabelSize;

            string label = Label;
            //Label can't be empty or the offset measured is zero
            if (label == "")
                label = " ";
            
            _LabelSize = font.MeasureString(this.Label);
            _LabelSizeMeasured = true; 
            return _LabelSize; 
        }

        public Vector2 GetInfoLabelSize(SpriteFont font)
        {
            if (_InfoLabelSizeMeasured)
                return _InfoLabelSize;

            if (this.Parent == null)
                return new Vector2(0, 0);

            string text = this.Parent.InfoLabel;
            //Label can't be empty or the offset measured is zero
            if (text == "")
                text = " ";

            _InfoLabelSize = font.MeasureString(text);
            _InfoLabelSizeMeasured = true;
            return _InfoLabelSize;
        }

        public Vector2 GetParentLabelSize(SpriteFont font)
        {
            if (_ParentLabelSizeMeasured)
                return _ParentLabelSize;

            if (this.Parent == null)
                return new Vector2(0, 0);

            if (this.Parent.Parent == null)
                return new Vector2(0, 0);


            string label = this.Parent.Parent.ToString();
            //Label can't be empty or the offset measured is zero
            if (label == "")
                label = " ";

            _ParentLabelSize = font.MeasureString(label);
            _ParentLabelSizeMeasured = true;
            return _LabelSize;
        }

        /// <summary>
        /// Draw the text for the location at the specified screen coordinates
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="font"></param>
        /// <param name="ScreenDrawPosition"></param>
        /// <param name="MagnificationFactor"></param>
        /// <param name="DirectionToVisiblePlane">The Z distance of the location to the plane viewed by user.</param>
        public void DrawLabel(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch,
                              Microsoft.Xna.Framework.Graphics.SpriteFont font,
                              Vector2 ScreenDrawPosition,
                              float MagnificationFactor,
                              int DirectionToVisiblePlane)
        {
            Vector2 offset = GetLabelSize(font);
            offset.X /= 2;
            offset.Y /= 2;

            StructureTypeObj type = this.Parent.Type; 
            
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color((byte)(type.Color.R / 4),
                                                                                                      (byte)(type.Color.G / 4),
                                                                                                      (byte)(type.Color.B / 4),
                                                                                                      255);
            
            if (this.OffEdge)
                color = new Microsoft.Xna.Framework.Color(255, 255, 255, 255);

            if (this.IsUnverifiedTerminal)
                color = new Microsoft.Xna.Framework.Color((byte)255 - type.Color.R,
                    (byte)255 - type.Color.G,
                    (byte)255 - type.Color.B,
                    255);

            float scale;
            if (this.TypeCode == 0) // A point
            {
                if (DirectionToVisiblePlane == 0)
                    scale = MagnificationFactor * AnnotationOverlay.LocationTextScaleFactor;
                else
                    scale = MagnificationFactor * AnnotationOverlay.ReferenceLocationTextScaleFactor;
            }
            else //A circle
            {
                if (DirectionToVisiblePlane == 0)
                    scale = (((float)Radius / offset.X) * MagnificationFactor) / 2;
                else
                {
                    float radius = (float)((this.Radius / 4) > 128 ? 128 : this.Radius / 4);
                    scale = (((float)radius / offset.X) * MagnificationFactor) / 2;
                }
            }

            spriteBatch.DrawString(font,
                Label,
                ScreenDrawPosition,
                color,
                0,
                offset,
                scale,
                SpriteEffects.None,
                0);

            //If we have a parent of our parent then include thier ID in small font
            if (this.Parent.Parent != null)
            {
                StructureTypeObj ParentType = this.Parent.Parent.Type;
                Vector2 ParentOffset = this.GetParentLabelSize(font);
                ParentOffset.X /= 2f;
                ParentOffset.Y /= 2f; 

                Microsoft.Xna.Framework.Color ParentColor = new Microsoft.Xna.Framework.Color(ParentType.Color.R,
                                                                                                          ParentType.Color.G,
                                                                                                          ParentType.Color.B,
                                                                                                          255);

                string ParentLabel = this.Parent.Parent.ToString();
                float ParentScale = scale / 1.5f;
                Vector2 ParentScreenDrawPosition = ScreenDrawPosition;

                //Position label above the label for the location
                ParentScreenDrawPosition.Y -= ((offset.Y * 2) * scale); 

                spriteBatch.DrawString(font,
                    ParentLabel,
                    ParentScreenDrawPosition,
                    ParentColor,
                    0,
                    ParentOffset,
                    ParentScale,
                    SpriteEffects.None,
                    0);

            }

            //If we have an additional label, include that information in small font
            if (this.Parent.InfoLabel != null)
            {
                Vector2 LabelOffset = GetInfoLabelSize(font);
                LabelOffset.X /= 2f;
                LabelOffset.Y /= 2f; 

                string AdditionalLabel = this.Parent.InfoLabel;
                float LabelScale = scale / 1.5f;
                Vector2 LabelScreenDrawPosition = ScreenDrawPosition;

                //Position label below the label for the location
                LabelScreenDrawPosition.Y += ((offset.Y * 2) * scale);

                spriteBatch.DrawString(font,
                    AdditionalLabel,
                    LabelScreenDrawPosition,
                    color,
                    0,
                    LabelOffset,
                    LabelScale,
                    SpriteEffects.None,
                    0);

            }

        }

        /// <summary>
        ///  Draws the background of the location, can be:
        ///  0: A box around label for a point location.
        ///  1: A circle around label for a circle location
        ///  
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="DirectionToVisiblePlane">The offset to the plane viewed by user.  
        /// We present different cues depending on where the mark is relative to the section the viewer is seeing</param>
        public void DrawBackground(GraphicsDevice graphicsDevice,
                                   int DirectionToVisiblePlane)
        {
            //Are we drawing a point?
            switch (this.TypeCode)
            {
                case LocationType.POINT:
                    DrawPointBackground(graphicsDevice, DirectionToVisiblePlane);
                    break;
                case LocationType.CIRCLE:
                    DrawCircleBackground(graphicsDevice, DirectionToVisiblePlane);
                    break;
                default:
                    Trace.WriteLine("Unimplemented location type, not drawn, ID: " + this.ID.ToString(), "WebAnnotation");
                    break; 
            }
        }


        public void DrawPointBackground(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                   int DirectionToVisiblePlane)
        {
            WebAnnotation.ViewModel.SectionAnnotationViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection((int)this.Z);
            if (sectionAnnotations == null)
                return; 

            Vector2 Offset = GetLabelSize(); 
            
            StructureTypeObj type = this.Parent.Type;
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(type.Color.R,
                type.Color.G,
                type.Color.B,
                128);

            GridVector2 Pos = sectionAnnotations.GetPositionForLocation(this.ID);

            VertexPositionColor[] verts;
            int[] indicies;
            int[] lineIndicies;
            
            //Location is in the visible section
            if (DirectionToVisiblePlane == 0)
            {
                Offset.X *= AnnotationOverlay.LocationTextScaleFactor / 2;
                Offset.Y *= AnnotationOverlay.LocationTextScaleFactor / 2; 

                verts = new VertexPositionColor[] { 
                        new VertexPositionColor(new Vector3((float)Pos.X - Offset.X, (float)Pos.Y - Offset.Y, 1), color),
                        new VertexPositionColor(new Vector3((float)Pos.X + Offset.X, (float)Pos.Y - Offset.Y, 1), color),
                        new VertexPositionColor(new Vector3((float)Pos.X + Offset.X, (float)Pos.Y + Offset.Y, 1), color),
                        new VertexPositionColor(new Vector3((float)Pos.X - Offset.X, (float)Pos.Y + Offset.Y, 1), color)};

                indicies = new int[] { 0, 2, 1, 0, 3, 2 };
                lineIndicies = new int[] { 0, 1, 2, 3, 0 };
            }
            else if (DirectionToVisiblePlane > 0)
            {
                Offset.X *= AnnotationOverlay.ReferenceLocationTextScaleFactor / 2;
                Offset.Y *= AnnotationOverlay.ReferenceLocationTextScaleFactor / 2; 

                verts = new VertexPositionColor[] { 
                            new VertexPositionColor(new Vector3((float)Pos.X - Offset.X, (float)Pos.Y - Offset.Y, 1), color),
                            new VertexPositionColor(new Vector3((float)Pos.X + Offset.X, (float)Pos.Y - Offset.Y, 1), color),
                            new VertexPositionColor(new Vector3((float)Pos.X, (float)Pos.Y + (Offset.Y * 1.5f), 1), color)};

                indicies = new int[] { 2, 1, 0 };
                lineIndicies = new int[] { 0, 1, 2, 0 };
            }
            else
            {
                Offset.X *= AnnotationOverlay.ReferenceLocationTextScaleFactor / 2;
                Offset.Y *= AnnotationOverlay.ReferenceLocationTextScaleFactor / 2; 

                verts = new VertexPositionColor[] { 
                            new VertexPositionColor(new Vector3((float)Pos.X + Offset.X, (float)Pos.Y + Offset.Y, 1), color),
                            new VertexPositionColor(new Vector3((float)Pos.X - Offset.X, (float)Pos.Y + Offset.Y, 1), color),
                            new VertexPositionColor(new Vector3((float)Pos.X, (float)Pos.Y - (Offset.Y * 1.5f), 1), color)};

                indicies = new int[] { 2, 1, 0 };
                lineIndicies = new int[] { 0, 1, 2, 0 };
            }

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList,
                                                                          verts,
                                                                          0,
                                                                          verts.Length,
                                                                          indicies,
                                                                          0,
                                                                          indicies.Length / 3);

            //Draw an opaque border around the background
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Color.A = 255;
            }

            

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineStrip,
                                                                          verts,
                                                                          0,
                                                                          verts.Length,
                                                                          lineIndicies,
                                                                          0,
                                                                          lineIndicies.Length - 1);
        }


        public void DrawCircleBackground(GraphicsDevice graphicsDevice,
                                   int DirectionToVisiblePlane)
        {

            StructureTypeObj type = this.Parent.Type;
            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(type.Color.R,
                type.Color.G,
                type.Color.B,
                128);

            DrawCircleBackground(graphicsDevice, DirectionToVisiblePlane, color);
        }

        /// <summary>
        /// The verticies should really be cached and handed up to LocationObjRenderer so all similiar objects can be rendered in one
        /// call.  This method is in the middle of a change from using triangles to draw circles to using textures. 
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="DirectionToVisiblePlane"></param>
        /// <param name="color"></param>
        public void DrawCircleBackground(GraphicsDevice graphicsDevice,
                                   int DirectionToVisiblePlane, Microsoft.Xna.Framework.Color color)
        {
            WebAnnotation.ViewModel.SectionAnnotationViewModel sectionAnnotations = AnnotationOverlay.GetAnnotationsForSection((int)this.Z);
            if (sectionAnnotations == null)
                return; 

            GridVector2 Pos = sectionAnnotations.GetPositionForLocation(this.ID); 

            //A better way to implement this is to just render a circle texture and add color using lighting, but 
            //this will work for now

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[ GlobalPrimitives.SquareVerts.Length ];
            GlobalPrimitives.SquareVerts.CopyTo(verts, 0); 

            //Can't populate until we've referenced CircleVerts
            int[] indicies = GlobalPrimitives.SquareIndicies;
            float radius = (float)this.Radius;

            if (DirectionToVisiblePlane != 0)
                radius = OffSectionRadius; 

            /*
            //Figure out if we should draw triangles instead
            if (DirectionToVisiblePlane == 0)
            {
                verts = new VertexPositionColor[GlobalPrimitives.CircleVerts.Length];
                GlobalPrimitives.CircleVerts.CopyTo(verts, 0);

                indicies = GlobalPrimitives.CircleVertIndicies;
                lineIndicies = GlobalPrimitives.CircleBorderIndicies;
            }
            else if (DirectionToVisiblePlane > 0)
            {
                verts = new VertexPositionColor[GlobalPrimitives.UpTriVerts.Length];
                GlobalPrimitives.UpTriVerts.CopyTo(verts, 0);
           
                indicies = new int[] { 2, 1, 0 };
                lineIndicies = new int[] { 0, 1, 2, 0 };
                radius = OffSectionRadius;
            }
            else
            {
                verts = new VertexPositionColor[GlobalPrimitives.DownTriVerts.Length];
                GlobalPrimitives.DownTriVerts.CopyTo(verts, 0);

                indicies = new int[] { 2, 1, 0 };
                lineIndicies = new int[] { 0, 1, 2, 0 };
                radius = OffSectionRadius;
            }
            */

            //Draw an opaque border around the background
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position.X *= radius;
                verts[i].Position.Y *= radius;

                verts[i].Position.X += (float)Pos.X;
                verts[i].Position.Y += (float)Pos.Y;
                verts[i].Color = color;
            }

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColorTexture>(PrimitiveType.TriangleList,
                                                                          verts,
                                                                          0,
                                                                          verts.Length,
                                                                          indicies,
                                                                          0,
                                                                          2);
               
            /*
            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleFan,
                                                                          verts,
                                                                          0,
                                                                          verts.Length,
                                                                          indicies,
                                                                          0,
                                                                          indicies.Length - 2);

            //Draw an opaque border around the background
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Color.A = 255;
            }

           

            graphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.LineStrip,
                                                                          verts,
                                                                          0,
                                                                          verts.Length,
                                                                          lineIndicies,
                                                                          0,
                                                                          lineIndicies.Length - 1);
             */
            
        }

        #endregion

        



        #region IUIObject Members

        public override ContextMenu ContextMenu
        {
            get
            {
                ContextMenu menu = new ContextMenu();

                
                menu.MenuItems.Add("Properties", ContextMenu_OnProperties);

                MenuItem menuExtensible = new MenuItem("Terminal", ContextMenu_OnTerminal);
                MenuItem menuOffEdge = new MenuItem("Off Edge", ContextMenu_OnOffEdge);
                MenuItem menuSeperator = new MenuItem(); 
                MenuItem menuDelete = new MenuItem("Delete", ContextMenu_OnDelete);

                menuExtensible.Checked = this.Data.Terminal;
                menuOffEdge.Checked = this.Data.OffEdge;              
                
                menu.MenuItems.Add(menuExtensible);
                menu.MenuItems.Add(menuOffEdge);
                menu.MenuItems.Add(menuSeperator); 
                menu.MenuItems.Add(menuDelete); 

                return menu; 
            }
        }

        public override Image SmallThumbnail
        {
            get { throw new NotImplementedException(); }
        }

        public override string ToolTip
        {
            get { throw new NotImplementedException(); }
        }

        public override void Save()
        {
            Store.Locations.Save();
        }

        public override Viking.UI.Controls.GenericTreeNode CreateNode()
        {
            return new Viking.UI.Controls.GenericTreeNode(this); 
        }

        public override int TreeImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        public override int TreeSelectedImageIndex
        {
            get { throw new NotImplementedException(); }
        }

        public override Type[] AssignableParentTypes
        {
            get { return new Type[] { typeof(StructureObj) }; }
        }

        public override void SetParent(IUIObject parent)
        {
            throw new NotImplementedException();
        }

        #endregion

        protected void ContextMenu_OnProperties(object sender, EventArgs e)
        {
            Viking.UI.Forms.PropertySheetForm.Show(this.Parent);
        }

        protected void ContextMenu_OnTerminal(object sender, EventArgs e)
        {
            DBACTION originalDBAction = this.DBAction; 
            this.Data.Terminal = !this.Data.Terminal;
            this.Data.DBAction = DBACTION.UPDATE;
            bool success = Store.Locations.Save();
            if (!success)
            {
                this.Data.Terminal = !this.Data.Terminal;
                this.DBAction = originalDBAction;
            }
        
        }

        protected void ContextMenu_OnOffEdge(object sender, EventArgs e)
        {
            DBACTION originalDBAction = this.DBAction; 
            this.Data.OffEdge = !this.Data.OffEdge;
            this.Data.DBAction = DBACTION.UPDATE;
            bool success = Store.Locations.Save();
            if (!success)
            {
                this.Data.OffEdge = !this.Data.OffEdge;
                this.DBAction = originalDBAction;
            }
        }

        protected void ContextMenu_OnDelete(object sender, EventArgs e)
        {
            Delete();
        }

        public override void Delete()
        {
            DBACTION originalAction = this.DBAction; 
            this.DBAction = DBACTION.DELETE;

            bool success = Store.Locations.Save();
            if(!success)
            {
                //Write straight to data since we have an assert to check whether an object is being deleted, but
                //in this case we know it is ok
                this.Data.DBAction = originalAction;
            }


            if (this.ParentID.HasValue)
                Store.Structures.CheckForOrphan(this.ParentID.Value);
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
    }
}
