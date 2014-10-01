using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Viking.VolumeModel
{
    /// <summary>
    /// A tile pyramid is a list of all tiles visible within a bounding box at each of the requested resolutions
    /// </summary>
    public class TilePyramid
    {
        /// <summary>
        /// The boundary of all tiles contained in the pyramid
        /// </summary>
        public readonly Geometry.GridRectangle Bounds; 

        /// <summary>
        /// A list of downsample levels, each entry is a sorted list using the tile names as the key and the tile object as data
        /// </summary>
        private SortedDictionary<int, SortedDictionary<string, Tile>> TilesAtLevel = new SortedDictionary<int, SortedDictionary<string, Tile>>();

        public TilePyramid(Geometry.GridRectangle bounds)
        {
            Bounds = bounds;
        }

        public void AddTile(int downsample, Tile tile)
        {
            SortedDictionary<string, Tile> tiles;

            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                tiles = new SortedDictionary<string, Tile>();
                tiles.Add(tile.ToString(), tile);
                TilesAtLevel.Add(downsample, tiles);
            }
            else
            {
                tiles = TilesAtLevel[downsample];
                Debug.Assert(false == tiles.ContainsKey(tile.ToString()));
                tiles.Add(tile.ToString(), tile);
            }
        }

        public void AddTiles(int downsample, IEnumerable<Tile> AddedTileArray)
        {
            SortedDictionary<string, Tile> tiles;

            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                tiles = new SortedDictionary<string, Tile>();
                TilesAtLevel.Add(downsample, tiles);
            }
            else
            {
                tiles = TilesAtLevel[downsample];
            }

            foreach (Tile t in AddedTileArray)
            {
                tiles.Add(t.ToString(), t);
            }
        }

        public SortedDictionary<string, Tile> GetTilesForLevel(int downsample)
        {
            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                return new SortedDictionary<string, Tile>();
            }

            return TilesAtLevel[downsample]; 
        }

        public int[] AvailableLevels
        {
            get 
            {
                int[] Levels = new int[TilesAtLevel.Keys.Count];
                TilesAtLevel.Keys.CopyTo(Levels, 0);
                return Levels; 
            }
        }
    }
}
