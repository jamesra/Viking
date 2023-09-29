using System.Collections.Generic;
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
        private readonly SortedDictionary<int, SortedDictionary<string, TileViewModel>> TilesAtLevel = new SortedDictionary<int, SortedDictionary<string, TileViewModel>>();

        public TilePyramid(Geometry.GridRectangle bounds)
        {
            Bounds = bounds;
        }

        public void AddTile(int downsample, TileViewModel tileViewModel)
        {
            SortedDictionary<string, TileViewModel> tiles;

            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                tiles = new SortedDictionary<string, TileViewModel>
                {
                    { tileViewModel.ToString(), tileViewModel }
                };
                TilesAtLevel.Add(downsample, tiles);
            }
            else
            {
                tiles = TilesAtLevel[downsample];
                Debug.Assert(false == tiles.ContainsKey(tileViewModel.ToString()));
                tiles.Add(tileViewModel.ToString(), tileViewModel);
            }
        }

        public void AddTiles(int downsample, IEnumerable<TileViewModel> AddedTileArray)
        {
            SortedDictionary<string, TileViewModel> tiles;

            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                tiles = new SortedDictionary<string, TileViewModel>();
                TilesAtLevel.Add(downsample, tiles);
            }
            else
            {
                tiles = TilesAtLevel[downsample];
            }

            foreach (TileViewModel t in AddedTileArray)
            {
                tiles.Add(t.ToString(), t);
            }
        }

        public SortedDictionary<string, TileViewModel> GetTilesForLevel(int downsample)
        {
            if (TilesAtLevel.ContainsKey(downsample) == false)
            {
                return new SortedDictionary<string, TileViewModel>();
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
