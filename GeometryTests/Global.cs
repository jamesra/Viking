using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;

namespace GeometryTests
{
    public static class Global
    {
        /// <summary>
        /// The seed value for FsCheck random generators
        /// </summary>
        //public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.StdGen.NewStdGen(1475755927,296717278);
        //public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.StdGen.NewStdGen(385597658, 296722803);
        //public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.StdGen.NewStdGen(1825931114, 296730464);

        public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.newSeed();
        public static void ResetRollingSeed()
        {
            _RollingStdGenSeed = StdGenSeed;
        }

        private static FsCheck.Random.StdGen _RollingStdGenSeed = StdGenSeed;
        public static FsCheck.Random.StdGen RollingStdGenSeed
        {
            get {
                var oldSeed = _RollingStdGenSeed;
                _RollingStdGenSeed = FsCheck.Random.stdNext(_RollingStdGenSeed).Item2;
                return oldSeed;
            }
        }

        
    }
}
