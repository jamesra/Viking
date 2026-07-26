using System.Collections.Generic;

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
        /// A list of downsample levels, each entry is a sorted list using the tile unique key and the tile object as data
        /// </summary>
        private readonly SortedDictionary<int, SortedDictionary<TileUniqueKey, TileViewModel>> TilesAtLevel = new();

        public void AddTile(int downsample, TileViewModel tileViewModel)
        {
            var key = tileViewModel.UniqueKey;
            if (TilesAtLevel.TryGetValue(downsample, out SortedDictionary<TileUniqueKey, TileViewModel> tiles))
            {
                if (tiles.ContainsKey(key))
                    return;
                tiles.Add(key, tileViewModel);
            }
            else
            {
                tiles = new SortedDictionary<TileUniqueKey, TileViewModel>
                {
                    { key, tileViewModel }
                };
                TilesAtLevel.Add(downsample, tiles);
            }
        }

        public void AddTiles(int downsample, IEnumerable<TileViewModel> AddedTileArray)
        {
            SortedDictionary<TileUniqueKey, TileViewModel> tiles;

            if (TilesAtLevel.TryGetValue(downsample, out var value))
            {
                tiles = value;
            }
            else
            {
                tiles = new SortedDictionary<TileUniqueKey, TileViewModel>();
                TilesAtLevel.Add(downsample, tiles);
            }

            foreach (TileViewModel t in AddedTileArray)
            {
                if (!tiles.ContainsKey(t.UniqueKey))
                    tiles.Add(t.UniqueKey, t);
            }
        }

        public SortedDictionary<TileUniqueKey, TileViewModel> GetTilesForLevel(int downsample) => TilesAtLevel.TryGetValue(downsample, out var level) ? level : new SortedDictionary<TileUniqueKey, TileViewModel>();

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
