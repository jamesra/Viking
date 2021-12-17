using System;
using Xunit;
using Viking.DataModel.Annotation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.Configuration;

namespace ConnectomeDataModelCoreTest
{
    public class DatabaseTest
    {
        private readonly AnnotationContext dbContext;
        private readonly DbContextOptions<AnnotationContext> dbContextOptions;

        public DatabaseTest(IConfiguration configuration)
        {  
            /* ContextOptions = new DbContextOptionsBuilder<Viking.DataModel.Annotation.AnnotationContext>()
                //.UseSqlite("Filename=Test.db")
                .UseSqlServer()
                .Options; */
            dbContextOptions = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(configuration.GetConnectionString("TestConnection"),
                    options => options.UseNetTopologySuite())
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .Options;
            
            Seed();
        }

        private void Seed()
        {
            using (var context = new AnnotationContext(dbContextOptions))
            {
                context.StructureTypes.Add(new StructureType() 
                    { Id = 1, Abstract = false, Code = "1", Username = "test", Color = 0 });

                context.SaveChanges();
            }
        }

        [Fact]
        public void Test1()
        {
            using (var context = new AnnotationContext(dbContextOptions))
            {
                var st = context.StructureTypes.Find(1);
                Xunit.Assert.IsType<StructureType>(st);
                Xunit.Assert.NotNull(st);
                Xunit.Assert.Equal(st.Id, 1);
            }
        }
    }
}
