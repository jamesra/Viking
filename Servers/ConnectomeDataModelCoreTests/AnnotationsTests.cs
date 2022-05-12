using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Viking.DataModel.Annotation;
using Viking.DataModel.Annotation.Tests;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /*
    public class AnnotationsTests : IClassFixture<CreateDropDatabaseFixture>
    {
        private readonly AnnotationContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger Log;

        #region Seeding
        public AnnotationsTests(CreateDropDatabaseFixture dbFixture, IConfiguration config, ILogger log = null)
        {
            _dbContext = dbFixture.DataContext;
            _config = config;
            Log = log;
        }

        protected DbContextOptions<AnnotationContext> ContextOptions { get; }
     
        private void Seed()
        {
            using (var context = new AnnotationContext(ContextOptions))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                var one = new Item("ItemOne");
                one.AddTag("Tag11");
                one.AddTag("Tag12");
                one.AddTag("Tag13");

                var two = new Item("ItemTwo");

                var three = new Item("ItemThree");
                three.AddTag("Tag31");
                three.AddTag("Tag31");
                three.AddTag("Tag31");
                three.AddTag("Tag32");
                three.AddTag("Tag32");

                context.AddRange(one, two, three);

                context.SaveChanges();
            }
        } 

        #endregion
     
        [Fact]
        public void GeometryCanLoad()
        {
            long Id = 3850;
            var loc = context.Locations.Find(Id);

            Console.WriteLine($"{loc.Id} - {loc.VolumeShape}");

            Id = 4; 
            loc = context.Locations.Find(Id);

            Console.WriteLine($"{loc.Id} - {loc.VolumeShape}");
        } 
    }
    */
}
