using System;

namespace ConnectomeDataModel
{
    public static class Configuration
    {
        public static void LoadNativeAssemblies(string rootApplicationPath)
        {
            SqlServerTypesLoader.Loader.LoadNativeAssemblies(rootApplicationPath);
        }
    }
}