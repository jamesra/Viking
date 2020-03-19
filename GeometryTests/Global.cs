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
        public static readonly FsCheck.Random.StdGen StdGenSeed = FsCheck.Random.newSeed();
    }
}
