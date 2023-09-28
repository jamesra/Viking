using System;
using System.IO;
using System.Reflection;

namespace Annotation
{
    public class AssemblyResolver
    {
        public AssemblyResolver(AppDomain domain)
        {
            domain.AssemblyResolve += OnResolveAssembly;
        }

        private Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            //This handler is called only when the common language runtime tries to bind to the assembly and fails.

            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            string applicationDirectory = Path.GetDirectoryName(executingAssembly.Location);

            string[] fields = args.Name.Split(',');
            string assemblyName = fields[0];
            string assemblyCulture;
            if (fields.Length < 2)
                assemblyCulture = null;
            else
                assemblyCulture = fields[2].Substring(fields[2].IndexOf('=') + 1);


            string assemblyFileName = assemblyName + ".dll";
            string assemblyPath;

            if (assemblyName.EndsWith(".resources"))
            {
                // Specific resources are located in app subdirectories
                string resourceDirectory = Path.Combine(applicationDirectory, assemblyCulture);

                assemblyPath = Path.Combine(resourceDirectory, assemblyFileName);
            }
            else
            {
                assemblyPath = Path.Combine(applicationDirectory, assemblyFileName);
            } 

            if (File.Exists(assemblyPath))
            {
                //Load the assembly from the specified path.                    
                Assembly loadingAssembly = Assembly.LoadFrom(assemblyPath);

                //Return the loaded assembly.
                return loadingAssembly;
            }
            else
            {
                return null;
            }
        }
    }
}
