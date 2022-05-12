using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; 
using Microsoft.EntityFrameworkCore.Design;

namespace Viking.DataModel.Annotation
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AnnotationContext>
    {
        public AnnotationContext CreateDbContext(string[] args)
        {
            //Check if the first argument is a connection string
            string connectionString =
                "Server=localhost;Database={0};Trusted_Connection=True;integrated security=false;Pwd=Glutamate88;User ID=sa;MultipleActiveResultSets=true";
            if (args.Length > 0)
            {
                connectionString = args[0];
            }

            var optionsBuilder = new DbContextOptionsBuilder<AnnotationContext>();
            optionsBuilder.UseSqlServer(connectionString,
                config => config.UseNetTopologySuite())
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
            return new AnnotationContext(optionsBuilder.Options);
        }
    }
}
