using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Annotation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AssemblyResolver resolver = new AssemblyResolver(AppDomain.CurrentDomain);

            ServiceHost serviceHost =
                new ServiceHost(typeof(AnnotateService));
             
    
            serviceHost.Open();
            Console.WriteLine(
                "Service running. Please 'Enter' to exit...");
            Console.ReadLine();
        }
    }
}
