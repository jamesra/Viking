using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Viking.DataModel.Annotation.Tests
{ 
    public class StructureTypeTests : IClassFixture<EmptyDatabaseFixture>
    { 
        private readonly AnnotationContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger Log;

        public StructureTypeTests(EmptyDatabaseFixture dbFixture, ILogger log = null)
        {
            _dbContext = dbFixture.DataContext; 
            Log = log;
        } 

        [Fact]
        public async Task StructureTypeBasics()
        {
            StructureType t = new StructureType()
            {
                Code = "C",
                Abstract = false,
                Name = "Cell",
                Color = 0x0000FF,
                HotKey = "C",
            };

            await _dbContext.StructureTypes.AddAsync(t, CancellationToken.None);

            await _dbContext.SaveChangesAsync();

            //clear the locally tracked objects
            _dbContext.ChangeTracker.Clear();

            var foundCell = await _dbContext.StructureTypes.FindAsync(t.Id);
            Assert.NotNull(foundCell);

            ///////////////////////////////
            ///Create a child StructureType
            ///////////////////////////////
            StructureType psd = new StructureType
            {
                Code = "PSD",
                Abstract = false,
                Name = "Post-Synaptic Density",
                Color = 0xFF0000,
                HotKey = "P",
                Parent = foundCell
            };

            await _dbContext.StructureTypes.AddAsync(psd);

            await _dbContext.SaveChangesAsync();

            //clear the locally tracked objects
            _dbContext.ChangeTracker.Clear();

            var foundChild = await _dbContext.StructureTypes.FindAsync(psd.Id);
            Assert.NotNull(foundChild); 

            /////////////////////////////////////////
            ///Try deleting the parent structure type
            /////////////////////////////////////////

            _dbContext.ChangeTracker.Clear();
            foundCell = _dbContext.StructureTypes.Find(foundCell.Id);
            _dbContext.StructureTypes.Remove(foundCell);
            try
            {
                Assert.Throws<DbUpdateException>(() => _dbContext.SaveChanges());
            }
            catch (DbUpdateException)
            {
            }

            //////////////////////////////////////////////////
            /// Try deleting child structure type, should work
            //////////////////////////////////////////////////

            _dbContext.StructureTypes.Remove(foundChild);
            int nChanges = _dbContext.SaveChanges();
            Assert.Equal(nChanges,2); //Remove child and parent from previous attempt
        }  
    }
}
