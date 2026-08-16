using FsCheck;

namespace GeometryTests.FSCheck
{
    /// <summary>
    /// Shared FsCheck configuration for Geometry.Core specs: pinned replay seed, modest sizes.
    /// </summary>
    internal static class CoreCheck
    {
        public static Configuration Config(string name)
        {
            Configuration configuration = Configuration.QuickThrowOnFailure;
            configuration.Replay = Global.StdGenSeed;
            configuration.MaxNbOfTest = 100;
            configuration.StartSize = 8;
            configuration.EndSize = 32;
            configuration.Name = name;
            configuration.QuietOnSuccess = true;
            return configuration;
        }

        public static void Run(Property property, string name) => property.Check(Config(name));
    }
}
