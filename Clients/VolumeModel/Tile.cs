using Geometry;
using System.Diagnostics;
using System.Linq;

namespace Viking.VolumeModel
{
    /// <summary>
    /// A tile is a combination of:
    ///     A unique identifier determined by texture file name
    ///     A texture, which may or may not be loaded
    ///     A set of verticies to position the tile in space
    /// </summary>
    public class Tile
    {
        /// <summary>
        /// Currently a combination of section number, transform, downsample, and texturename
        /// </summary>
        public readonly string UniqueKey;

        public static string CreateUniqueKey(int Section, string Transform, string Channel, int Downsample, string TextureName)
        {
            //return "S: " + Section.ToString("D4") + " T: " + Transform + " C: " + Channel + " DS: " + Downsample.ToString("D3") + " T: " + TextureName;
            return $"S: {Section:D04} T: {Transform} C: {Channel} DS: {Downsample:D03} T: {TextureName}";
        }
        
        /// <summary>
        /// Cache this because we'll use it a lot
        /// </summary>
        private readonly int _UniqueKeyHashcode;
        public override int GetHashCode()
        {
            return _UniqueKeyHashcode;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Tile t))
                return false;

            //This is faster than a full string test
            if (t.GetHashCode() != this.GetHashCode())
                return false;

            return t.UniqueKey == this.UniqueKey;
        }
        
      //  public static string ConstructUniqueKey(int SectionNumber, string Texture
        public readonly PositionNormalTextureVertex[] Verticies;

        /// <summary>
        /// Indicies passed to render call specifying triangle verticies
        /// </summary>
        public readonly int[] TriangleIndicies = null;

        /// <summary>
        /// Size of the tile in memory
        /// </summary>
        public readonly int Size;

        /// <summary>
        /// Size the texture will require
        /// </summary>
        public int TextureSize => (int)(Bounds.Width / this.Downsample * (Bounds.Height / this.Downsample)); //This is a rough estimate, not exact

        /// <summary>
        /// The amount the tile has been downsampled by
        /// </summary>
        public readonly int Downsample;    

        public readonly string TextureFullPath;
        public readonly string TextureCacheFilePath;

        //Need to initialize or union operator of bounds property won't work
        public readonly GridRectangle Bounds;

        //PORT: public readonly string TextureCachedFileName;

        //PORT: This is a viewModel concept
        //private int MipMapLevels = 1;

        public Tile(in string UniqueKey, 
                                in PositionNormalTextureVertex[] verticies,
                                in int[] TriangleIndicies,
                                in string TextureFullPath,
                                in string cachePath,
                                in int downsample)
        {
            this.UniqueKey = UniqueKey;
            this._UniqueKeyHashcode = UniqueKey.GetHashCode();

            this.Size = (verticies.Length * 8 * 8) + (TriangleIndicies.Length * 4) + TextureFullPath.Length + UniqueKey.Length;
            
            this.Downsample = downsample;
            this.TextureFullPath = TextureFullPath.Replace('\\','/');
            this.TextureCacheFilePath = cachePath.Replace('/','\\');

            Bounds = verticies.Select(v => v.Position.XY()).BoundingBox();
            //PORT: this.TextureCachedFileName = cachedTextureFileName;
            //PORT: this.MipMapLevels = mipMapLevels;

            this.Verticies = verticies;
            Debug.Assert(verticies != null);
            Debug.Assert(verticies.Length > 0, "Tile must have verticies");
            this.TriangleIndicies = TriangleIndicies; 
        }

        public static PositionNormalTextureVertex[] CalculateVerticies(ITransformControlPoints transform, Geometry.Transforms.TileTransformInfo info)
        {
            PositionNormalTextureVertex[] verticies = new PositionNormalTextureVertex[transform.MapPoints.Length];

            for (int i = 0; i < transform.MapPoints.Length; i++)
            {
                GridVector2 CtrlP = transform.MapPoints[i].ControlPoint;
                GridVector2 MapP = transform.MapPoints[i].MappedPoint;

                var pos = new GridVector3(CtrlP.X, CtrlP.Y, 0);
                var tex = new GridVector2(MapP.X / info.ImageWidth, MapP.Y / info.ImageHeight);
                var norm = GridVector3.UnitZ;

                verticies[i] = new PositionNormalTextureVertex(pos, norm, tex);

                Debug.Assert(verticies[i].Texture.X >= 0 && verticies[i].Texture.X <= 1, "Texture X coordinate out of bounds 0,1");
                Debug.Assert(verticies[i].Texture.Y >= 0 && verticies[i].Texture.Y <= 1, "Texture Y coordinate out of bounds 0,1");

                //verticies[i] = new PositionNormalTextureVertex(position, GridVector3.UnitZ, textureCoord);
            }

            return verticies;
        }

        public override string ToString()
        {
            return UniqueKey;
        }
    }
}
