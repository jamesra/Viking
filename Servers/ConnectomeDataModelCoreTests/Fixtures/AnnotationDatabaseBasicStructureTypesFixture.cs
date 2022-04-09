using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation.Tests.BuiltIn;
using Xunit;

namespace Viking.DataModel.Annotation.Tests
{
    namespace BuiltIn
    {
        public enum StructureTypes
        {
            /// <summary>
            /// Cell, the root of the structure type tree
            /// </summary>
            Cell,
            /// <summary>
            /// Post-synaptic density
            /// </summary>
            PSD,
            /// <summary>
            /// Conventional Synapse
            /// </summary>
            CS,
            /// <summary>
            /// Gap junction
            /// </summary>
            G
        }
    }
    

    /// <summary>
    /// Populates an annotation database with some common structure types required for tests.
    /// </summary>
    public class AnnotationDatabaseBasicStructureTypesFixture
    {
        public readonly AnnotationContext DataContext;
        private readonly IContextBuilder<AnnotationContext> _contextBuilder;

        public AnnotationDatabaseBasicStructureTypesFixture(IContextBuilder<AnnotationContext> dbContextBuilder)
        {
            DataContext = dbContextBuilder.DataContext;
            _contextBuilder = dbContextBuilder;

            PopulateStructureTypes();
        }

        public Dictionary<StructureTypes, StructureType> StructureTypes = new Dictionary<StructureTypes, StructureType>()
        {
            {BuiltIn.StructureTypes.Cell, new StructureType { Code = "C", Name = "Cell" } },
            {BuiltIn.StructureTypes.PSD, new StructureType { Code = nameof(BuiltIn.StructureTypes.PSD), Name = "Post-Synaptic Density" }},
            {BuiltIn.StructureTypes.CS, new StructureType { Code = nameof(BuiltIn.StructureTypes.CS), Name = "Conventional Synapse" }},
            {BuiltIn.StructureTypes.G, new StructureType { Code = nameof(BuiltIn.StructureTypes.G), Name = "Gap Junction" }}
        };

        public StructureType CellType => StructureTypes[BuiltIn.StructureTypes.Cell];
        public StructureType PSDType => StructureTypes[BuiltIn.StructureTypes.PSD];
        public StructureType CSType => StructureTypes[BuiltIn.StructureTypes.CS];
        public StructureType GType => StructureTypes[BuiltIn.StructureTypes.G];

        private void PopulateStructureTypes()
        {
            var Cell = StructureTypes[BuiltIn.StructureTypes.Cell];
            DataContext.StructureTypes.Add(Cell);

            foreach (var child in StructureTypes.Values)
            {
                if (child == Cell) continue;

                child.Parent = Cell;
                DataContext.StructureTypes.Add(child);
            }

            DataContext.SaveChanges();
        }
         
    }
}