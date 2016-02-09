using System;

namespace XNATestbed
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (XNATestBedMain game = new XNATestBedMain())
            {
                game.Run();
            }
        }
    }
#endif
}

