using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Viking.VolumeModel;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    
    public enum DrawWhichPoly
    {
        PREVPOLY,
        NEXTPOLY
    }

    class RetraceAndReplacePathCommand : PlaceCurveWithPenCommand
    {
        //Variables:

        //If we make a wrong intersection, this will track the index of that wrong intersection
        private int WrongIntersectionPoint;

        //Original Polygons
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        //Our original polygon plus the origin of retrace and replace and the origin point index
        GridPolygon VolumePolygonPlusOrigin;
        public PointIndex OriginIndex;

        //Meshes of the individual cut pieces of the retrace and replace
        PositionColorMeshModel PrevWalkMesh = null;
        PositionColorMeshModel NextWalkMesh = null;

        //Each of the cut pieces in polygon forms
        private GridPolygon NextWalkPolygon = null;
        private GridPolygon PrevWalkPolygon = null;

        //The output polygons we create
        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;
        public GridPolygon SmoothedVolumePolygon;

        //Is the command ready to finish if we try to?
        private bool IsReadyToComplete;

        //False draws the PrevWalkPolygon, true draws the NextWalkPolygon
        private DrawWhichPoly DrawPoly;


        //Curve Interpolations Variable
        public override uint NumCurveInterpolations
        {
            get
            {
                return Global.NumClosedCurveInterpolationPoints;
            }
        }

        //Section to Volume Mapper
        public Viking.VolumeModel.IVolumeToSectionTransform mapping;

        //Replace and retrace constructor
        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
            IsReadyToComplete = false;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            
            this.OriginIndex = GridPolygon.AddPointToPolygon(this.OriginalVolumePolygon, origin, out this.VolumePolygonPlusOrigin);
            
            if (VolumePolygonPlusOrigin.ExteriorRing.Contains(origin))
            {
                foreach (GridVector2[] ring in OriginalVolumePolygon.InteriorRings)
                {
                    VolumePolygonPlusOrigin.AddInteriorRing(ring);
                }
            }
            SmoothedVolumePolygon = VolumePolygonPlusOrigin.Smooth(Global.NumClosedCurveInterpolationPoints);
        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color.ToXNAColor(), origin, LineWidth, false, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            this.OriginIndex = GridPolygon.AddPointToPolygon(this.OriginalVolumePolygon, origin, out this.VolumePolygonPlusOrigin);
            SmoothedVolumePolygon = VolumePolygonPlusOrigin.Smooth(Global.NumClosedCurveInterpolationPoints);
        }
        
        /// <summary>
        /// If the pen path changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(!PenPathIsValid(out SortedDictionary<double, PointIndex> IntersectingVerts))
            {
                return;
            }

            //If our intersection was found
            if(IntersectingVerts.Count > 0)
            {
                //Get the location of the intersection
                PointIndex FirstIntersect = IntersectingVerts.Values.First();
                
                //If the intersection found and the origin where we started are on the same ring, allow retrace/replace to take place
                if(this.OriginIndex.AreOnSameRing(FirstIntersect))
                {  
                    
                    //Chop off the most recently drawn endpoint of the path because it won't be included in final path
                    //PenInput.Pop();

                    //Simplify our path
                    //PenInput.SimplifiedPath = CurveSimplificationExtensions.DouglasPeuckerReduction(PenInput.Path, Global.PenSimplifyThreshold);

                    NextWalkPolygon = WalkPolygonCut(this.OriginIndex, FirstIntersect, this.VolumePolygonPlusOrigin, RotationDirection.COUNTERCLOCKWISE, PenInput.Path);
                    PrevWalkPolygon = WalkPolygonCut(this.OriginIndex, FirstIntersect, this.VolumePolygonPlusOrigin, RotationDirection.CLOCKWISE, PenInput.Path);


                    GridPolygon OriginalVolumePolyExterior = new GridPolygon(OriginalVolumePolygon.ExteriorRing);
                    //If an expansion, then figure out which is larger, make that the green mesh, and set the smaller poly and mesh to null. Otherwise display the two polygons.
                    SetMeshes(OriginalVolumePolyExterior);
                    
                    //The Retrace is finished, allow OnMouseUp to run.
                    IsReadyToComplete = true;   
                }
            }

            this.Parent.Invalidate();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            //If the command is in a valid state populate the output poly and call execute. Otherwise deactive the command.
            if (IsReadyToComplete)
            {
                if (!OriginIndex.IsInner)
                {
                    if (NextWalkMesh != null && DrawPoly == DrawWhichPoly.NEXTPOLY)
                    {
                        OutputVolumePolygon = AddInteriorToCutPolygon(OriginalVolumePolygon, NextWalkPolygon);
                    }
                    else if (PrevWalkMesh != null && DrawPoly == DrawWhichPoly.PREVPOLY)
                    {
                        OutputVolumePolygon = AddInteriorToCutPolygon(OriginalVolumePolygon, PrevWalkPolygon);
                    }
                }
                else
                {
                    OutputVolumePolygon = new GridPolygon(OriginalVolumePolygon.ExteriorRing);
                    OutputVolumePolygon = AddInteriorToCutPolygon(OriginalVolumePolygon, OutputVolumePolygon);

                    if (NextWalkMesh != null && DrawPoly == DrawWhichPoly.NEXTPOLY)
                    {
                        OutputVolumePolygon.AddInteriorRing(NextWalkPolygon);
                    }
                    else if (PrevWalkMesh != null && DrawPoly == DrawWhichPoly.PREVPOLY)
                    {
                        OutputVolumePolygon.AddInteriorRing(PrevWalkPolygon);
                    }
                }

                try
                {
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                this.Execute();
            }

            base.OnMouseUp(sender, e);
        }

        private bool PenPathIsValid(out SortedDictionary<double, PointIndex> IntersectingVerts)
        {
            IntersectingVerts = VolumePolygonPlusOrigin.IntersectingSegments(PenInput.LastSegment);

            //Condition Check to make sure pen path exists and is valid
            if (PenInput == null || PenInput.Path.Count < 3 || OriginalVolumePolygon.TotalVerticies <= 3)
            {
                return false;
            }

            //If a wrong intersection was made with another polygon or pen path intersected itself make the user return back to the point of the error
            if (WrongIntersectionPoint > 0 && WrongIntersectionPoint < PenInput.Path.Count)
            {
                return false;
            }
            else
            {
                WrongIntersectionPoint = -1;
                LineColor = LineColor = Microsoft.Xna.Framework.Color.Blue;
            }
             
            //Does the path self intersect? Does the path cross another ring it shouldn't?
            if (((IntersectingVerts.Count > 0 && !this.OriginIndex.AreOnSameRing(IntersectingVerts.Values.First())) || PenInput.Segments.IntersectionPoint(PenInput.LastSegment, true, out GridLineSegment? IntersectedSegment).HasValue) && WrongIntersectionPoint == -1)
            {
                IsReadyToComplete = false;

                LineColor = Microsoft.Xna.Framework.Color.Red;

                WrongIntersectionPoint = PenInput.Path.Count - 1;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="start_index"></param>
        /// <param name="originPolygon">The polygon to cut</param>
        /// <param name="direction">The direction we will walk to connect the starting and ending cut points</param>
        /// <param name="cutLine">The line cutting the polygon.  It should intersect the same polygonal ring in two locations without intersecting any others</param>
        /// <param name="FirstIntersect">The polygon vertex before the intersected segment, use intersect_index.next to get the endpoint of the intersected segment of the polygon</param>
        /// <returns></returns>
        protected static GridPolygon WalkPolygonCut(PointIndex start_index,  GridPolygon originPolygon, RotationDirection direction, IList<GridVector2> cutLine)
        {
            //Find a possible intersection point for the retrace
            GridLineSegment polygonCutLine = new GridLineSegment(cutLine[1], cutLine[0]);
            SortedDictionary<double, PointIndex> IntersectingVerts = originPolygon.IntersectingSegments(polygonCutLine);
            
            //No intersection with the cut line
            if (IntersectingVerts.Count == 0)
            {
                throw new ArgumentException("Last segment of cutLine must intersect a polygon ring");
            }

            PointIndex intersect_index = IntersectingVerts.Values.First();
            if (false == intersect_index.AreOnSameRing(start_index))
            {
                throw new ArgumentException("Cut must run between the same ring of the polygon without intersecting other rings");
            }

            return WalkPolygonCut(start_index,
                                  intersect_index,
                                  originPolygon,
                                  direction,
                                  cutLine);
        }



        protected static GridPolygon WalkPolygonCut(PointIndex start_index, PointIndex intersect_index, GridPolygon originPolygon, RotationDirection direction, IList<GridVector2> cutLine)
        {
            if (false == intersect_index.AreOnSameRing(start_index))
            {
                throw new ArgumentException("Cut must run between the same ring of the polygon without intersecting other rings");
            }

            //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
            List<GridVector2> walkedPoints = new List<GridVector2>();
            PointIndex current = start_index;
            
            GridVector2 PolyIntersectionPoint;
            {
                //Add the intersection point of where we crossed the boundary
                GridLineSegment polygonCutLine = new GridLineSegment(cutLine[1], cutLine[0]);
                GridLineSegment intersected_segment = new GridLineSegment(intersect_index.Point(originPolygon), intersect_index.Next.Point(originPolygon));
                bool intersected = polygonCutLine.Intersects(intersected_segment, out PolyIntersectionPoint);
                if(false == intersected)
                {
                    throw new ArgumentException("Last segment of cutLine must intersect a polygon ring");
                }
            }

            PointIndex end_Index = (direction == RotationDirection.COUNTERCLOCKWISE) ? intersect_index : intersect_index.Next;

            do
            {
                Debug.Assert(walkedPoints.Contains(current.Point(originPolygon)) == false);
                walkedPoints.Add(current.Point(originPolygon));
                if (direction == RotationDirection.COUNTERCLOCKWISE)
                    current = current.Next;
                else
                    current = current.Previous;

            }
            while (current != end_Index);

            walkedPoints.Add(current.Point(originPolygon));

            //Add the intersection point of where we crossed the boundary
            if (GridVector2.DistanceSquared(PolyIntersectionPoint, walkedPoints.Last()) > Geometry.Global.EpsilonSquared)
            {
                Debug.Assert(walkedPoints.Contains(PolyIntersectionPoint) == false);
                walkedPoints.Add(PolyIntersectionPoint);
            }
            else
            {
                int i = 5; //Temp for debugging
            }

            List<GridVector2> SimplifiedPath = CurveSimplificationExtensions.DouglasPeuckerReduction(cutLine, Global.PenSimplifyThreshold);

            //The intersection point marks where we enter the polygon.  The first point in the path is not added because it indicates where the line exited the cut region. 
            //Add the PenInput.Path 

            //Temp for debugging ///////////////
            for (int iCut = 0; iCut < SimplifiedPath.Count; iCut++)
            {
                Debug.Assert(walkedPoints.Contains(SimplifiedPath[iCut]) == false);
                if (GridVector2.DistanceSquared(SimplifiedPath[iCut], walkedPoints.Last()) <= Geometry.Global.EpsilonSquared) 
                {
                    int i = 5; //Temp for debugging
                    continue;
                }

                walkedPoints.Add(SimplifiedPath[iCut]);
            }
            /////////////////////////////////////
            ///
            //walkedPoints.AddRange(cutLine);

            Debug.Assert(walkedPoints.RemoveDuplicates().Length == walkedPoints.Count-1);

            if(direction == RotationDirection.CLOCKWISE)
            {
                walkedPoints.Reverse();
            }

            return new GridPolygon(walkedPoints.EnsureClosedRing());
        }

        /// <summary>
        /// Sets meshes for retrace and replace
        /// </summary>
        /// <returns></returns>
        private void SetMeshes(GridPolygon OriginalVolumePolyExterior)
        {
            if ((!OriginIndex.IsInner && (NextWalkPolygon.Area > OriginalVolumePolyExterior.Area || PrevWalkPolygon.Area > OriginalVolumePolyExterior.Area)) || (OriginIndex.IsInner && (NextWalkPolygon.Area > OriginalVolumePolygon.InteriorPolygons[OriginIndex.iInnerPoly.Value].Area || PrevWalkPolygon.Area > OriginalVolumePolygon.InteriorPolygons[OriginIndex.iInnerPoly.Value].Area)))
            {
                if (NextWalkPolygon.Area > PrevWalkPolygon.Area)
                {
                    NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(NextWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    PrevWalkMesh = null;
                    DrawPoly = DrawWhichPoly.NEXTPOLY;
                }
                else
                {
                    PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(PrevWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    NextWalkMesh = null;
                    DrawPoly = DrawWhichPoly.PREVPOLY;
                }
            }
            else
            {
                NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(NextWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(PrevWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));
                DrawPoly = DrawWhichPoly.NEXTPOLY;
            }
        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {

        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }


        protected GridPolygon AddInteriorToCutPolygon(GridPolygon originalPoly, GridPolygon cutPoly)
        {
            int? originRing = OriginIndex.iInnerPoly;
            int i = 0;
            foreach (GridVector2[] interiorRing in originalPoly.InteriorRings)
            {
                if(cutPoly.Contains(interiorRing[0]))
                {
                    if(originRing.HasValue)
                    {
                        if(i != originRing)
                            cutPoly.AddInteriorRing(interiorRing);
                    }
                    else
                        cutPoly.AddInteriorRing(interiorRing);
                }
                i++;
            }

            return cutPoly;
        }

        /// <summary>
        /// Can a control point be placed or the command completed by clicking the mouse at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanControlPointBePlaced(GridVector2 WorldPos)
        {
            return (!OverlapsAnyVertex(WorldPos));
        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(GridVector2 WorldPos)
        {
            //Does the path self intersect
            if (PenInput.Segments.SelfIntersects())
                return false;
            
            return ShapeIsValid();
        }

        protected override bool ShapeIsValid()
        {
            if (this.Verticies.Length < 3 || curve_verticies == null || this.curve_verticies.ControlPoints.Length < 3)
                return false;

            try
            {
                return this.curve_verticies.ControlPoints.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            base.OnDraw(graphicsDevice, scene, basicEffect);

            if (PrevWalkMesh != null || NextWalkMesh != null)
            {
                if (NextWalkMesh != null && PrevWalkMesh != null) // If control is pressed
                {
                    /*
                    double DistanceA = NextWalkPolygon.Distance(PenInput.cursor_position);
                    double DistanceB = PrevWalkPolygon.Distance(PenInput.cursor_position);
                    if(DistanceA < DistanceB)
                    */
                    if(Control.ModifierKeys.CtrlPressed())
                    {
                        NextWalkMesh.Color = Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f);
                        PrevWalkMesh.Color = Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f);
                        DrawPoly = DrawWhichPoly.NEXTPOLY;

                    }
                    else
                    {
                        NextWalkMesh.Color = Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f);
                        PrevWalkMesh.Color = Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f);
                        DrawPoly = DrawWhichPoly.PREVPOLY;
                    }
                }

                if (NextWalkMesh == null)
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, new PositionColorMeshModel[] { PrevWalkMesh });
                else if (PrevWalkMesh == null)
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, new PositionColorMeshModel[] { NextWalkMesh });
                else
                    MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, new PositionColorMeshModel[] { PrevWalkMesh, NextWalkMesh });
            }
        }
    }
}