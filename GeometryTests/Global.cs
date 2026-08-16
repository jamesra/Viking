namespace GeometryTests
{
    public static class Global
    {
        /// <summary>
        /// Fixed seed so CI failures replay. Override Replay on a Configuration when shrinking a specific case.
        /// </summary>
        public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.StdGen.NewStdGen(1475755927, 296717278);
        public static void ResetRollingSeed() => _RollingStdGenSeed = StdGenSeed;

        private static FsCheck.Random.StdGen _RollingStdGenSeed = StdGenSeed;
        public static FsCheck.Random.StdGen RollingStdGenSeed
        {
            get
            {
                var oldSeed = _RollingStdGenSeed;
                _RollingStdGenSeed = FsCheck.Random.stdNext(_RollingStdGenSeed).Item2;
                return oldSeed;
            }
        }


    }
}
