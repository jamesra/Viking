using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Viking.DataModel.Annotation.Tests.BuiltIn;
using Xunit;

namespace Viking.DataModel.Annotation.Tests
{
    public class PermittedStructureLinksTests : IClassFixture<AnnotationDatabaseBasicStructureTypesFixture>
    {
        private readonly AnnotationContext _dbContext;
        private readonly ILogger Log;
        private readonly AnnotationDatabaseBasicStructureTypesFixture _fixture;

        public PermittedStructureLinksTests(AnnotationDatabaseBasicStructureTypesFixture dbFixture, ILogger log = null)
        {
            _dbContext = dbFixture.DataContext;
            _fixture = dbFixture;
            Log = log;
        }

        [Fact]
        public async Task PermittedStructureLinkBasics()
        {
            var synapseLink = new PermittedStructureLink
            {
                SourceType = _fixture.CSType,
                TargetType = _fixture.PSDType,
                Bidirectional = false
            };

            var gapJunctionLink = new PermittedStructureLink
            {
                SourceType = _fixture.GType,
                TargetType = _fixture.GType,
                Bidirectional = true
            };

            _dbContext.PermittedStructureLinks.Add(synapseLink);
            _dbContext.PermittedStructureLinks.Add(gapJunctionLink); 

            
        }
    }
}