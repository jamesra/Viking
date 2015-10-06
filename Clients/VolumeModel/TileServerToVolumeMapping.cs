using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Diagnostics;
using Geometry;
using Geometry.Transforms; 

namespace Viking.VolumeModel
{
    /// <summary>
    /// For this mapping the tiles are registered into the volume and we can use simple math to figure out which tiles to load.  However our transform mapping 
    /// needs to be able to map points to mosaic space for annotations
    /// </summary>
    public class TileServerToVolumeMapping : TileServerMapping
    {
        

        /// <summary>
        /// The transformation which will/has converted the tiles from section space into volume space.
        /// This can be null if this section is not warped into volume space. 
        /// </summary>
        public readonly TriangulationTransform VolumeTransform;

        public TileServerToVolumeMapping(Section section, string name, TileServerMapping ToWarp, TriangulationTransform Transform)
            : base(ToWarp, section, name)
        {
            this.VolumeTransform = Transform;
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
        

        public override TilePyramid VisibleTiles(GridRectangle VisibleBounds, double DownSample)
        {
            int roundedDownsample = NearestAvailableLevel(DownSample);

            GridQuad VisibleQuad = null; 

            //Add any corners of the VisibleBounds that we can transform to the list of points
            List<MappingGridVector2> VisiblePoints = VisibleBoundsCorners(VolumeTransform, VisibleBounds);
            if (VisiblePoints.Count != 4)
            {
                //If we can't map all four corners then add all the points from the transform falling inside the visible rectangle or 
                //connected to those points by an edge
                List<MappingGridVector2> listTransformPoints = this.VolumeTransform.IntersectingControlRectangle(VisibleBounds, true);
                VisiblePoints.AddRange(listTransformPoints);
            }
            else
            {
                VisiblePoints.Sort(new MappingGridVector2SortByMapPoints());
                VisibleQuad = new GridQuad(VisiblePoints[0].MappedPoint,
                                           VisiblePoints[1].MappedPoint,
                                           VisiblePoints[2].MappedPoint,
                                           VisiblePoints[3].MappedPoint); 
            }

            //OK, transform all points falling inside the section border
            //Starting with low-res tiles, add tiles to the list until we reach desired resolution
            //List<Tile> TilesToDraw = new List<Tile>();
            TilePyramid TilesToDraw = new TilePyramid(VisibleBounds);

            if (VisiblePoints.Count < 3)
                return TilesToDraw;

            GridRectangle SectionBorder = MappingGridVector2.CalculateMappedBounds(VisiblePoints.ToArray());

            int iLevel = AvailableLevels.Length - 1;
            int level = AvailableLevels[iLevel];
            do
            {
                List<Tile> newTiles = RecursiveVisibleTiles(SectionBorder,
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


        private List<Tile> RecursiveVisibleTiles(GridRectangle SectionVisibleBounds,
                                                 GridQuad VisibleQuad, 
                                                 int roundedDownsample)
        {
            GridInfo gridInfo = LevelToGridInfo[roundedDownsample];

            
            long ScaledTileSizeX = this.TileSizeX * roundedDownsample;
            long ScaledTileSizeY = this.TileSizeY * roundedDownsample;

            //Figure out which grid locations are visible
            int iMinX = (int)Math.Floor(SectionVisibleBounds.Left / ScaledTileSizeX);
            int iMinY = (int)Math.Floor(SectionVisibleBounds.Bottom / ScaledTileSizeY);
            int iMaxX = (int)Math.Ceiling(SectionVisibleBounds.Right / ScaledTileSizeX);
            int iMaxY = (int)Math.Ceiling(SectionVisibleBounds.Top / ScaledTileSizeY);

            iMinX = iMinX < 0 ? 0 : iMinX;
            iMinY = iMinY < 0 ? 0 : iMinY;
            iMaxX = iMaxX > gridInfo.GridXDim ? gridInfo.GridXDim : iMaxX;
            iMaxY = iMaxY > gridInfo.GridYDim ? gridInfo.GridYDim : iMaxY;

            int ExpectedTileCount = (iMaxX - iMinX) * (iMaxY - iMinY);
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);


            for (int iX = iMinX; iX < iMaxX; iX++)
            {
                for (int iY = iMinY; iY < iMaxY; iY++)
                {
                    //Figure out if the tile would be visible
                    GridRectangle tileBorder = TileBorder(iX, iY, roundedDownsample);
                    if (tileBorder.Intersects(SectionVisibleBounds) == false)
                        continue; 

                    
                    //If we have a visble quad see if the tile intersects that too
                    if (VisibleQuad != null)
                    {
                        if (VisibleQuad.Contains(tileBorder) == false)
                            continue;
                    }
                    string UniqueID = Tile.CreateUniqueKey(Section.Number, "Grid to Volume", Name, roundedDownsample, this.TileTextureFileName(iX, iY));
                    
                    //                   Trace.WriteLine(TextureFileName, "VolumeModel"); 
                    Tile tile = Global.TileCache.Fetch(UniqueID);
                    if (tile == null)
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
                                                            this.TileTextureCacheFileName(roundedDownsample, iX, iY),
                                                            //PORT: TextureCacheFileName,
                                                            this.Name,
                                                            roundedDownsample,
                                                            MipMapLevels);
                    }
                    
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
        private GridRectangle TileBorder(int iX, int iY, int Downsample)
        {
            GridRectangle TileBorder;
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double X = iX * Width;
            double Y = iY * Height;

            TileBorder = new GridRectangle(X, X+Width, Y, Y+Height);

            return TileBorder; 
        }

        GridVector2[] TileHull(int iX, int iY, int Downsample)
        {
            GridVector2[] verts = new GridVector2[16];
            double Width = this.TileSizeX * Downsample;
            double Height = this.TileSizeY * Downsample;
            double HalfWidth = Width / 2;
            double HalfHeight = Height / 2;
            double QuarterWidth = HalfWidth / 2;
            double QuarterHeight = HalfHeight / 2; 
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

           // verts[8] = new GridVector2(X + HalfWidth, Y + HalfHeight);
             

            return verts;
        }

        protected PositionNormalTextureVertex[] CalculateVerticies(int iX,
                                                                            int iY,
                                                                            int Downsample,
                                                                            out int[] TriangleEdges)
        {
            GridVector2[] SectionTileCorners = TileHull(iX,iY,Downsample);
            List<MappingGridVector2> TileCornerMappedPoints = new List<MappingGridVector2>(4);
            
            bool transformSuccess = false; 
            GridVector2 mappedVert;
            for(int i = 0; i < SectionTileCorners.Length; i++)
            {
                GridVector2 Vert = SectionTileCorners[i];
                transformSuccess = VolumeTransform.TryTransform(Vert, out mappedVert);
                if(transformSuccess)
                {
                    TileCornerMappedPoints.Add(new MappingGridVector2(mappedVert, Vert)); 
                }
            }

            GridRectangle tileBorder = TileBorder(iX,iY,Downsample);

            List<MappingGridVector2> MappedPoints = new List<MappingGridVector2>(16);

            //Add all of the points in the tiles rectangle
            
            MappedPoints.AddRange(VolumeTransform.IntersectingMappedRectangle(tileBorder, false));

//            MappedPoints.Sort(new MappingGridVector2SortByMapPoints());

            if (MappedPoints.Count + TileCornerMappedPoints.Count < 3)
            {
                TriangleEdges = new int[0];
                return new PositionNormalTextureVertex[0];
            }

            //Eliminate duplicates in case tile coordinate landed exactly on transform grid (Common for 0,0)
            for(int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
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
            for(int iPoint = 0; iPoint < MappedPoints.Count; iPoint++)
            {
                DelaunayPoints[iPoint] = MappedPoints[iPoint].MappedPoint;
            }

            try
            {
                TriangleEdges = Geometry.Delaunay.Triangulate(DelaunayPoints);//, SectionTileCorners, false);
                //MappedPoints.AddRange(TileCornerMappedPoints); 
            }
            catch (ArgumentException )
            {
                //This can occur if all the points are on a straight line
                TriangleEdges = new int[0];
                return new PositionNormalTextureVertex[0];
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
