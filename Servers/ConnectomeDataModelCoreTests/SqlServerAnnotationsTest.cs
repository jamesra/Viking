using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Viking.DataModel.Annotation.Tests
{ 
    public class SqlServerAnnotationsTest : IClassFixture<CreateDropDatabaseFixture>
    { 
        private readonly AnnotationContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger Log;

        public SqlServerAnnotationsTest(CreateDropDatabaseFixture dbFixture, IConfiguration config, ILogger log = null)
        {
            _dbContext = dbFixture.DataContext;
            _config = config;
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
                Color = 0x0000FF
            };

            await _dbContext.StructureTypes.AddAsync(t, CancellationToken.None);

            await _dbContext.SaveChangesAsync();
        }
    }
}
