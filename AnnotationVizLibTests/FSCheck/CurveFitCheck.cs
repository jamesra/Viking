using FsCheck;

namespace AnnotationVizLibTests.FSCheck
{
    /// <summary>
    /// Shared FsCheck configuration for AnnotationVizLib properties: pinned replay seed, modest sizes.
    /// </summary>
    internal static class CurveFitCheck
    {
        public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.StdGen.NewStdGen(1475755927, 296717278);

        public static Configuration Config(string name)
        {
            Configuration configuration = Configuration.QuickThrowOnFailure;
            configuration.Replay = StdGenSeed;
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
