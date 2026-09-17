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
            AssemblyResolver resolver = new(AppDomain.CurrentDomain);

            ServiceHost serviceHost =
                new(typeof(AnnotateService));


            serviceHost.Open();
            Console.WriteLine(
                "Service running. Please 'Enter' to exit...");
            Console.ReadLine();
        }
    }
}
