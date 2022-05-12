using Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    public class TileGridToVolumeMapping : TileGridMapping
    {
        //protected GridTransform GridToVolumeTransform;

        /// <summary>
        /// The transformation which will/has converted the tiles from section space into volume space.
        /// This can be null if this section is not warped into volume space. 
        /// </summary>
        public readonly ITransform VolumeTransform;

        public TileGridToVolumeMapping(Section section, string name, TileGridMapping ToWarp, ITransform Transform)
            : base(ToWarp, section, name)
        {
            this.VolumeTransform = Transform;
            this._XYScale = ToWarp.XYScale;

            /*
            //Create a single grid transform for all tiles
            GridToVolumeTransform = new GridTransform();

            GridInfo gridInfo = LevelToGridInfo[this.MinDownsample];

            MappingGridVector2[] mappingPoints = new MappingGridVector2[(gridInfo.GridYDim+1) * (gridInfo.GridXDim+1)];
            int[] TriangleIndicies = new int[gridInfo.GridYDim * gridInfo.GridXDim * 6];
            
            int iPoint = 0;
            int iTriangle = 0; 
            for(int iX = 0; iX <= gridInfo.GridXDim; iX++)
            {
                for(int iY = 0; iY <= gridInfo.GridYDim; iY++, iPoint++)
                {
                    GridVector2 controlPoint = new GridVector2(iX * this.TileSizeX,
                                                               iY * this.TileSizeY); 
                    GridVector2 mappedPoint = controlPoint; //This will get warped later when we add to volume transform
                    MappingGridVector2 PointPair = new MappingGridVector2(controlPoint, mappedPoint);

                    mappingPoints[iPoint] = PointPair;
                    
                    if(iY < gridInfo.GridYDim &&
                       iX < gridInfo.GridXDim)
                    {
                        TriangleIndicies[iTriangle++] = iPoint;
                        TriangleIndicies[iTriangle++] = iPoint + 1;
                        TriangleIndicies[iTriangle++] = iPoint + gridInfo.GridYDim + 1;

                        TriangleIndicies[iTriangle++] = iPoint + 1;
                        TriangleIndicies[iTriangle++] = iPoint + gridInfo.GridYDim + 1;
                        TriangleIndicies[iTriangle++] = iPoint + gridInfo.GridYDim + 2;
                    }
                }
            }

            //Todo: If we add the mapping points from the volume transform here they can be included in the output verticies

            GridToVolumeTransform.SetPointsAndTriangles(mappingPoints, TriangleIndicies); 

//            GridToVolumeTransform.Add(VolumeTransform); 
             */
        }

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            return this.VolumeTransform.TryTransform(P, out transformedP);
        }

        public override bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            return this.VolumeTransform.TryInverseTransform(P, out transformedP);
        }

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(in GridVector2[] P, out GridVector2[] transformedP)
        {
            return this.VolumeTransform.TryTransform(P, out transformedP);
        }

        public override bool[] TryVolumeToSection(in GridVector2[] P, out GridVector2[] transformedP)
        {
            return this.VolumeTransform.TryInverseTransform(P, out transformedP);
        }


        public override GridVector2[] VolumeToSection(GridVector2[] P)
        {
            return this.VolumeTransform.InverseTransform(P);
        }


        public override GridVector2[] SectionToVolume(GridVector2[] P)
        {
            return this.VolumeTransform.Transform(P);
        }

        public override Task FreeMemory()
        {
            if (VolumeTransform is IMemoryMinimization memMin)
            {
                memMin.MinimizeMemory();
            }

            return base.FreeMemory();
        }


        public override TilePyramid VisibleTiles(in GridRectangle VisibleBounds, double DownSample)
        {
            //double AdjustedDownSample = AdjustDownsampleForScale(DownSample);
            TilePyramid TilesToDraw = new TilePyramid(VisibleBounds);

            int roundedDownsample = NearestAvailableLevel(DownSample);
            if (roundedDownsample == int.MaxValue)
                return TilesToDraw;


            GridQuad VisibleQuad;
            GridRectangle? visibleSection = VisibleBounds.ApproximateVisibleMosaicBounds(this);
            if (!visibleSection.HasValue)
            {
                //Nothing to draw
                return TilesToDraw;
            }

            VisibleQuad = new GridQuad(visibleSection.Value);

            GridRectangle SectionBorder = visibleSection.Value;

            int iLevel = AvailableLevels.Length - 1;
            int level = AvailableLevels[iLevel];
            do
            {
                List<Tile> newTiles = RecursiveVisibleTiles(VisibleBounds,
                                                            SectionBorder,
                                                            VisibleQuad,
                                                            level
                                                            //PORT: AsynchTextureLoad
                                                            );

                //Insert at the beginning so we overwrite earlier tiles with poorer resolution
                TilesToDraw.AddTiles(level, newTiles.ToArray());

                iLevel--;
                if (iLevel >= 0)
                    level = AvailableLevels[iLevel];
            }
            while (level >= roundedDownsample && iLevel >= 0);

            //  Trace.WriteLine("Drawing " + TilesToDraw.Count.ToString() + " Tiles", "VolumeModel"); 

            return TilesToDraw;
        }


        private List<Tile> RecursiveVisibleTiles(
                                                 in GridRectangle VolumeVisibleBounds,
                                                 in GridRectangle SectionVisibleBounds,
                                                 GridQuad? VisibleQuad,
                                                 int roundedDownsample)
        {

            GridInfo gridInfo = LevelToGridInfo[roundedDownsample];

            int ScaledTileSizeX = this.TileSizeX * (int)roundedDownsample;
            int ScaledTileSizeY = this.TileSizeY * (int)roundedDownsample;

            //Figure out which grid locations are visible
            int iMinX = (int)Math.Floor(SectionVisibleBounds.Left / ScaledTileSizeX);
            int iMinY = (int)Math.Floor(SectionVisibleBounds.Bottom / ScaledTileSizeY);
            int iMaxX = (int)Math.Ceiling(SectionVisibleBounds.Right / ScaledTileSizeX);
            int iMaxY = (int)Math.Ceiling(SectionVisibleBounds.Top / ScaledTileSizeY);

            iMinX = iMinX < 0 ? 0 : iMinX;
            iMinY = iMinY < 0 ? 0 : iMinY;
            iMaxX = iMaxX < 0 ? 0 : iMaxX;
            iMaxY = iMaxY < 0 ? 0 : iMaxY;
            iMaxX = iMaxX > gridInfo.GridXDim ? gridInfo.GridXDim : iMaxX;
            iMaxY = iMaxY > gridInfo.GridYDim ? gridInfo.GridYDim : iMaxY;
            iMinX = iMinX > iMaxX ? iMaxX : iMinX;
            iMinY = iMinY > iMaxY ? iMaxY : iMinY;

            int ExpectedTileCount = (iMaxX - iMinX) * (iMaxY - iMinY);
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);


            for (int iX = iMinX; iX < iMaxX; iX++)
            {
                for (int iY = iMinY; iY < iMaxY; iY++)
                {
                    //Figure out if the tile would be visible
                    GridRectangle tileBorder = TileBoundingBox(iX, iY, (int)roundedDownsample);
                    if (tileBorder.Intersects(SectionVisibleBounds) == false)
                        continue;
                     
                    //If we have a visble quad see if the tile intersects that too
                    if (VisibleQuad.HasValue)
                    {
                        if (VisibleQuad.Value.Contains(tileBorder) == false)
                            continue;
                    }

                    string UniqueID = Tile.CreateUniqueKey(Section.Number, "Grid to Volume", Name, roundedDownsample, this.TileTextureFileName(iX, iY));

                    //                   Trace.WriteLine(TextureFileName, "VolumeModel"); 
                    Tile tile = Global.TileCache.Fetch(UniqueID);
                    if (tile == null && Global.TileCache.ContainsKey(UniqueID) == false)
                    {
                        //First create a new tile
                        int MipMapLevels = 1; //No mip maps
                        if (roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                            MipMapLevels = 0; //Generate mipmaps for lowest res texture

                        //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
                        int[] edges;
                        //                        Trace.WriteLine(TextureFileName, "VolumeModel");
                        PositionNormalTextureVertex[] verticies = CalculateVerticies(iX,
                                                                                     iY,
                                                                                     roundedDownsample,
                                                                                     out edges);


                        string TextureFileName = TileFullPath(iX, iY, roundedDownsample);

                        tile = Global.TileCache.ConstructTile(UniqueID,
                                                            verticies,
                                                            edges,
                                                            TextureFileName,
                                                            this.TileFullPath(iX, iY, roundedDownsample),
                                                            //PORT: TextureCacheFileName,
                                                            this.Name,
                                                            (int)roundedDownsample,
                                                            MipMapLevels);
                    }

                    if (tile != null)
                        TilesToDraw.Add(tile);
                }
            }

            return TilesToDraw;
        }

        /// <summary>
        /// Returns true if the specified tile is visible
        /// </summary>
        /// <param name="iX"></param>
        /// <param name="iY"></param>
        /// <returns></returns>
        private GridRectangle TileBoundingBox(int iX, int iY, int Downsample)
        {
            GridRectangle TileBorder;
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double X = iX * Width;
            double Y = iY * Height;

            TileBorder = new GridRectangle(X, X + Width, Y, Y + Height);

            return TileBorder;
        }

        GridVector2[] TileHull(int iX, int iY, int Downsample)
        {
            GridVector2[] verts = new GridVector2[16];
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double HalfWidth = Width / 2.0;
            double HalfHeight = Height / 2.0;
            double QuarterWidth = HalfWidth / 2.0;
            double QuarterHeight = HalfHeight / 2.0;
            double X = iX * Width;
            double Y = iY * Height;
            verts[0] = new GridVector2(X, Y);
            verts[1] = new GridVector2(X + Width, Y);
            verts[2] = new GridVector2(X, Y + Height);
            verts[3] = new GridVector2(X + Width, Y + Height);

            verts[4] = new GridVector2(X + HalfWidth, Y);
            verts[5] = new GridVector2(X + QuarterWidth, Y);
            verts[6] = new GridVector2(X + HalfWidth + QuarterWidth, Y);


            verts[7] = new GridVector2(X, Y + HalfHeight);
            verts[8] = new GridVector2(X, Y + QuarterHeight);
            verts[9] = new GridVector2(X, Y + HalfHeight + QuarterHeight);


            verts[10] = new GridVector2(X + Width, Y + QuarterHeight);
            verts[11] = new GridVector2(X + Width, Y + HalfHeight);
            verts[12] = new GridVector2(X + Width, Y + HalfHeight + QuarterHeight);

            verts[13] = new GridVector2(X + QuarterHeight, Y + Height);
            verts[14] = new GridVector2(X + HalfWidth, Y + Height);
            verts[15] = new GridVector2(X + HalfWidth + QuarterHeight, Y + Height);

            //verts[16] = new GridVector2(X + HalfWidth, Y + HalfHeight);

            // verts[8] = new GridVector2(X + HalfWidth, Y + HalfHeight);


            return verts;
        }

        GridVector2[] TileGrid(int iX, int iY, int GridDimX, int GridDimY, int Downsample)
        {
            GridVector2[] verts = new GridVector2[(GridDimX + 1) * (GridDimY + 1)];
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double XOrigin = iX * Width;
            double YOrigin = iY * Height;

            double XStep = Width / (double)GridDimX;
            double YStep = Height / (double)GridDimY;

            for (int jY = 0; jY <= GridDimY; jY++)
            {
                double Y = YOrigin + (YStep * (double)jY);
                for (int jX = 0; jX <= GridDimX; jX++)
                {
                    int i = (jY * (GridDimX + 1)) + jX;
                    double X = XOrigin + (XStep * (double)jX);

                    verts[i] = new GridVector2(X, Y);
                }
            }

            return verts;
        }

        protected PositionNormalTextureVertex[] CalculateVerticies(int iX,
                                                                            int iY,
                                                                            int Downsample,
                                                                            out int[] TriangleEdges)
        {
            //GridVector2[] SectionTileCorners = TileGrid(iX,iY,3,3,Downsample);
            GridVector2[] SectionTileCorners = TileHull(iX, iY, Downsample);
            List<MappingGridVector2> TileCornerMappedPoints = new List<MappingGridVector2>(SectionTileCorners.Length);

            GridVector2[] mappedVerts;
            bool[] transformSuccess = VolumeTransform.TryTransform(SectionTileCorners, out mappedVerts);

            for (int i = 0; i < SectionTileCorners.Length; i++)
            {
                if (transformSuccess[i])
                {
                    TileCornerMappedPoints.Add(new MappingGridVector2(mappedVerts[i], SectionTileCorners[i]));
                }
            }

            GridRectangle tileBorder = TileBoundingBox(iX, iY, Downsample);

            List<MappingGridVector2> MappedPoints = new List<MappingGridVector2>(16);

            //Add all of the points in the tiles rectangle

            if (VolumeTransform as ITransformControlPoints != null)
            {
                MappedPoints.AddRange(((ITransformControlPoints)VolumeTransform).IntersectingMappedRectangle(tileBorder));
            }

            //            MappedPoints.Sort(new MappingGridVector2SortByMapPoints());

            if (MappedPoints.Count + TileCornerMappedPoints.Count < 3)
            {
                TriangleEdges = Array.Empty<int>();
                return Array.Empty<PositionNormalTextureVertex>();
            }

            /*            if (TileCornerMappedPoints.Count < 3)
                        {
                            TriangleEdges = new int[0];
                            return new VertexPositionNormalTexture[0];
                        }
                        */


            /*
            for (int iPoint = 1; iPoint < MappedPoints.Count; iPoint++)
            {
                if (MappedPoints[iPoint].MappedPoint == MappedPoints[iPoint - 1].MappedPoint)
                {
                    iPoint--;
                    MappedPoints.RemoveAt(iPoint);
                }
            }
             */

            //Eliminate duplicates in case tile coordinate landed exactly on transform grid (Common for 0,0)
            for (int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
            {
                for (int iBoundPoint = 0; iBoundPoint < TileCornerMappedPoints.Count; iBoundPoint++)
                {
                    if (MappedPoints[iPoint].MappedPoint == TileCornerMappedPoints[iBoundPoint].MappedPoint)
                    {
                        MappedPoints.RemoveAt(iPoint);
                        iPoint--;
                        break;
                    }
                }
            }

            MappedPoints.AddRange(TileCornerMappedPoints);
            MappedPoints.Sort(new MappingGridVector2SortByMapPoints());

            GridVector2[] DelaunayPoints = new GridVector2[MappedPoints.Count];
            //Triangulate the points
            for (int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
            {
                DelaunayPoints[iPoint] = MappedPoints[iPoint].MappedPoint;
            }

            try
            {
                TriangleEdges = Geometry.Delaunay2D.Triangulate(DelaunayPoints);//, SectionTileCorners, false);
                //MappedPoints.AddRange(TileCornerMappedPoints); 
            }
            catch (ArgumentException)
            {
                //This can occur if all the points are on a straight line
                TriangleEdges = Array.Empty<int>();
                return Array.Empty<PositionNormalTextureVertex>();
            }

            //Ok, create all the verticies
            PositionNormalTextureVertex[] verticies = new PositionNormalTextureVertex[MappedPoints.Count];
            for (int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
            {
                GridVector2 Pos = MappedPoints[iPoint].ControlPoint;
                GridVector2 TextureBasis = MappedPoints[iPoint].MappedPoint;
                GridVector2 TexturePos = new GridVector2(((TextureBasis.X - tileBorder.Left) / tileBorder.Width),
                                                 ((TextureBasis.Y - tileBorder.Bottom) / tileBorder.Height));
                verticies[iPoint] = new PositionNormalTextureVertex(new GridVector3((float)Pos.X, (float)Pos.Y, 0),
                                                                     GridVector3.UnitZ,
                                                                     TexturePos);
            }

            return verticies;
        }
    }
}
