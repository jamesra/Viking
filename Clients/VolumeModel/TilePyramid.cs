using System.Collections.Generic;
using System.Diagnostics;

namespace Viking.VolumeModel
{
    /// <summary>
    /// A tile pyramid is a list of all tiles visible within a bounding box at each of the requested resolutions
    /// </summary>
    public class TilePyramid(Geometry.GridRectangle bounds)
    {
        /// <summary>
        /// The boundary of all tiles contained in the pyramid
        /// </summary>
        public readonly Geometry.GridRectangle Bounds = bounds;

        /// <summary>
        /// A list of downsample levels, each entry is a sorted list using the tile names as the key and the tile object as data
        /// </summary>
        private readonly SortedDictionary<int, SortedDictionary<string, TileViewModel>> TilesAtLevel = [];

        public void AddTile(int downsample, TileViewModel tileViewModel)
        { 
            if (TilesAtLevel.TryGetValue(downsample, out SortedDictionary<string, TileViewModel> tiles))
            {
                Debug.Assert(false == tiles.ContainsKey(tileViewModel.ToString()));
                tiles.Add(tileViewModel.ToString(), tileViewModel);
            }
            else
            {
                tiles = new SortedDictionary<string, TileViewModel>
                {
                    { tileViewModel.ToString(), tileViewModel }
                };
                TilesAtLevel.Add(downsample, tiles);
            }
        }

        public void AddTiles(int downsample, IEnumerable<TileViewModel> AddedTileArray)
        {
            SortedDictionary<string, TileViewModel> tiles;

            if (TilesAtLevel.TryGetValue(downsample, out var value))
            {
                tiles = value;
            }
            else
            {
                tiles = [];
                TilesAtLevel.Add(downsample, tiles);
            }

            foreach (TileViewModel t in AddedTileArray)
            {
                tiles.Add(t.ToString(), t);
            }
        }

        public SortedDictionary<string, TileViewModel> GetTilesForLevel(int downsample) => TilesAtLevel.TryGetValue(downsample, out var level) ? level : [];

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
