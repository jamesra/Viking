using Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Tileset plus volume stos for annotation mapping. ControlBounds stays the mosaic grid — do not use it to fit a volume camera.
    /// </summary>
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

            MappingVector2[] mappingPoints = new MappingVector2[(gridInfo.GridYDim+1) * (gridInfo.GridXDim+1)];
            int[] TriangleIndicies = new int[gridInfo.GridYDim * gridInfo.GridXDim * 6];
            
            int iPoint = 0;
            int iTriangle = 0; 
            for(int iX = 0; iX <= gridInfo.GridXDim; iX++)
            {
                for(int iY = 0; iY <= gridInfo.GridYDim; iY++, iPoint++)
                {
                    Vector2 controlPoint = new Vector2(iX * this.TileSizeX,
                                                               iY * this.TileSizeY); 
                    Vector2 mappedPoint = controlPoint; //This will get warped later when we add to volume transform
                    MappingVector2 PointPair = new MappingVector2(controlPoint, mappedPoint);

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

        public override bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) => this.VolumeTransform.TryTransform(P, out transformedP);

        public override bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) => this.VolumeTransform.TryInverseTransform(P, out transformedP);

        public override bool[] TrySectionToVolume(in Vector2[] P, out Vector2[] transformedP) => this.VolumeTransform.TryTransform(P, out transformedP);

        public override bool[] TryVolumeToSection(in Vector2[] P, out Vector2[] transformedP) => this.VolumeTransform.TryInverseTransform(P, out transformedP);


        public override Vector2[] VolumeToSection(Vector2[] P) => this.VolumeTransform.InverseTransform(P);


        public override Vector2[] SectionToVolume(Vector2[] P) => this.VolumeTransform.Transform(P);

        public override Task FreeMemory()
        {
            if (VolumeTransform is IMemoryMinimization memMin)
            {
                memMin.MinimizeMemory();
            }

            return base.FreeMemory();
        }


        public override TilePyramid VisibleTiles(Rectangle VisibleBounds, double DownSample)
        {
            //double AdjustedDownSample = AdjustDownsampleForScale(DownSample);
            TilePyramid TilesToDraw = new(VisibleBounds);

            int roundedDownsample = NearestAvailableLevel(DownSample);
            if (roundedDownsample == int.MaxValue)
                return TilesToDraw;


            Quad VisibleQuad;
            Rectangle? visibleSection = VisibleBounds.ApproximateVisibleMosaicBounds(this);
            if (!visibleSection.HasValue)
            {
                //Nothing to draw
                return TilesToDraw;
            }

            VisibleQuad = new Quad(visibleSection.Value);

            Rectangle SectionBorder = visibleSection.Value;

            int iLevel = AvailableLevels.Length - 1;
            int level = AvailableLevels[iLevel];
            do
            {
                List<TileViewModel> newTiles = RecursiveVisibleTiles(VisibleBounds,
                                                            SectionBorder,
                                                            VisibleQuad,
                                                            level
                                                            //PORT: AsynchTextureLoad
                                                            );

                //Insert at the beginning so we overwrite earlier tiles with poorer resolution
                TilesToDraw.AddTiles(level, [.. newTiles]);

                iLevel--;
                if (iLevel >= 0)
                    level = AvailableLevels[iLevel];
            }
            while (level >= roundedDownsample && iLevel >= 0);

            //  Trace.WriteLine("Drawing " + TilesToDraw.Count.ToString() + " Tiles", "VolumeModel"); 

            return TilesToDraw;
        }


        private List<TileViewModel> RecursiveVisibleTiles(
                                                 Rectangle VolumeVisibleBounds,
                                                 Rectangle SectionVisibleBounds,
                                                 Quad? VisibleQuad,
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
            List<TileViewModel> TilesToDraw = new(ExpectedTileCount);


            for (int iX = iMinX; iX < iMaxX; iX++)
            {
                for (int iY = iMinY; iY < iMaxY; iY++)
                {
                    TileKey tilekey = new(iX, iY, roundedDownsample);
                    if (TileTasks.ContainsKey(tilekey))
                        continue; //We are already getting this tile, so continue

                    //Figure out if the tile would be visible
                    Rectangle tileBorder = TileBoundingBox(iX, iY, (int)roundedDownsample);
                    if (tileBorder.Intersects(SectionVisibleBounds) == false)
                        continue;

                    //If we have a visble quad see if the tile intersects that too
                    if (VisibleQuad.HasValue)
                    {
                        if (VisibleQuad.Value.Contains(tileBorder) == false)
                            continue;
                    }

                    var UniqueID = TileUniqueKey.Create(Section.Number, "Grid to Volume", Name, roundedDownsample, this.TileTextureFileName(iX, iY));

                    //                   Trace.WriteLine(TextureFileName, "VolumeModel"); 
                    ;
                    if (Global.TileCache.TryGetValue(UniqueID, out TileViewModel tileViewModel) && tileViewModel != null)
                    {
                        TilesToDraw.Add(tileViewModel);
                    }
                    else
                    {
                        //Create a task to fetch the tile
                        Task<CreateTileTaskResult> tileTask = Task.Run<CreateTileTaskResult>(() =>
                            CreateTile(UniqueID, tilekey, this.Name));
                        TileTasks.TryAdd(tilekey, tileTask);
                        tileTask.ContinueWith(previousTask => OnTileCreated(previousTask.Result));
                    }
                }
            }

            return TilesToDraw;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uniqueID"></param>
        /// <param name="tileKey"></param>
        /// <param name="textureFilename"></param>
        /// <param name="name"></param>
        /// <param name="MipMapLevels">Ignored, lowest res texture gets mipmaps.  No others do (They are covered by lower-res textures)</param>
        /// <returns></returns>
        private async Task<CreateTileTaskResult> CreateTile(TileUniqueKey uniqueID, TileKey tileKey, string name)
        {
            int mipMapLevels;
            //First create a new tile 
            int roundedDownsample = tileKey.Downsample;
            int iX = tileKey.X;
            int iY = tileKey.Y;

            if (roundedDownsample == this.AvailableLevels[AvailableLevels.Length - 1])
                mipMapLevels = 0; //Generate mipmaps for lowest res texture
            else
                mipMapLevels = 1; //No mipmaps

            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
            //                        Trace.WriteLine(TextureFileName, "VolumeModel");
            PositionNormalTextureVertex[] verticies = CalculateVerticies(iX,
                iY,
                roundedDownsample,
                out int[] edges);


            string textureFileName = TileFullPath(iX, iY, roundedDownsample);

            var tileViewModel = Global.TileCache.ConstructTile(uniqueID,
                verticies,
                edges,
                textureFileName,
                textureFileName,
                //PORT: TextureCacheFileName,
                this.Name,
                (int)roundedDownsample,
                mipMapLevels);

            return new CreateTileTaskResult(tileViewModel, tileKey);
        }

        /// <summary>
        /// Returns true if the specified tile is visible
        /// </summary>
        /// <param name="iX"></param>
        /// <param name="iY"></param>
        /// <returns></returns>
        private Rectangle TileBoundingBox(int iX, int iY, int Downsample)
        {
            Rectangle TileBorder;
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double X = iX * Width;
            double Y = iY * Height;

            TileBorder = new Rectangle(X, X + Width, Y, Y + Height);

            return TileBorder;
        }

        Vector2[] TileHull(int iX, int iY, int Downsample)
        {
            Vector2[] verts = new Vector2[16];
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double HalfWidth = Width / 2.0;
            double HalfHeight = Height / 2.0;
            double QuarterWidth = HalfWidth / 2.0;
            double QuarterHeight = HalfHeight / 2.0;
            double X = iX * Width;
            double Y = iY * Height;
            verts[0] = new Vector2(X, Y);
            verts[1] = new Vector2(X + Width, Y);
            verts[2] = new Vector2(X, Y + Height);
            verts[3] = new Vector2(X + Width, Y + Height);

            verts[4] = new Vector2(X + HalfWidth, Y);
            verts[5] = new Vector2(X + QuarterWidth, Y);
            verts[6] = new Vector2(X + HalfWidth + QuarterWidth, Y);


            verts[7] = new Vector2(X, Y + HalfHeight);
            verts[8] = new Vector2(X, Y + QuarterHeight);
            verts[9] = new Vector2(X, Y + HalfHeight + QuarterHeight);


            verts[10] = new Vector2(X + Width, Y + QuarterHeight);
            verts[11] = new Vector2(X + Width, Y + HalfHeight);
            verts[12] = new Vector2(X + Width, Y + HalfHeight + QuarterHeight);

            verts[13] = new Vector2(X + QuarterHeight, Y + Height);
            verts[14] = new Vector2(X + HalfWidth, Y + Height);
            verts[15] = new Vector2(X + HalfWidth + QuarterHeight, Y + Height);

            //verts[16] = new Vector2(X + HalfWidth, Y + HalfHeight);

            // verts[8] = new Vector2(X + HalfWidth, Y + HalfHeight);


            return verts;
        }

        Vector2[] TileGrid(int iX, int iY, int GridDimX, int GridDimY, int Downsample)
        {
            Vector2[] verts = new Vector2[(GridDimX + 1) * (GridDimY + 1)];
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

                    verts[i] = new Vector2(X, Y);
                }
            }

            return verts;
        }

        protected PositionNormalTextureVertex[] CalculateVerticies(int iX,
                                                                            int iY,
                                                                            int Downsample,
                                                                            out int[] TriangleEdges)
        {
            //Vector2[] SectionTileCorners = TileGrid(iX,iY,3,3,Downsample);
            Vector2[] SectionTileCorners = TileHull(iX, iY, Downsample);
            List<MappingVector2> TileCornerMappedPoints = new(SectionTileCorners.Length);

            bool[] transformSuccess = VolumeTransform.TryTransform(SectionTileCorners, out Vector2[] mappedVerts);

            for (int i = 0; i < SectionTileCorners.Length; i++)
            {
                if (transformSuccess[i])
                {
                    TileCornerMappedPoints.Add(new MappingVector2(mappedVerts[i], SectionTileCorners[i]));
                }
            }

            Rectangle tileBorder = TileBoundingBox(iX, iY, Downsample);

            List<MappingVector2> MappedPoints = new(16);

            //Add all of the points in the tiles rectangle

            if (VolumeTransform as ITransformControlPoints != null)
            {
                MappedPoints.AddRange(((ITransformControlPoints)VolumeTransform).IntersectingMappedRectangle(tileBorder));
            }

            //            MappedPoints.Sort(new MappingVector2SortByMapPoints());

            if (MappedPoints.Count + TileCornerMappedPoints.Count < 3)
            {
                TriangleEdges = [];
                return [];
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

            MappedPoints.AddRange(TileCornerMappedPoints);

            //Eliminate duplicates in case tile coordinate landed exactly on transform grid (Common for 0,0)
            MappingVector2.RemoveMappedSpaceDuplicates(MappedPoints);

            MappedPoints.Sort(new MappingVector2SortByMapPoints());

            Vector2[] DelaunayPoints = new Vector2[MappedPoints.Count];
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
                TriangleEdges = [];
                return [];
            }

            //Ok, create all the verticies
            PositionNormalTextureVertex[] verticies = new PositionNormalTextureVertex[MappedPoints.Count];
            for (int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
            {
                Vector2 Pos = MappedPoints[iPoint].ControlPoint;
                Vector2 TextureBasis = MappedPoints[iPoint].MappedPoint;
                Vector2 TexturePos = new(((TextureBasis.X - tileBorder.Left) / tileBorder.Width),
                                                 ((TextureBasis.Y - tileBorder.Bottom) / tileBorder.Height));
                verticies[iPoint] = new PositionNormalTextureVertex(new Vector3((float)Pos.X, (float)Pos.Y, 0),
                                                                     Vector3.UnitZ,
                                                                     TexturePos);
            }

            return verticies;
        }
    }
}
