using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Annotation.Interfaces
{
    public interface IStructureType : IEquatable<IStructureType>
    {
        ulong ID { get; }
        ulong? ParentID { get; }
        string Name { get; }
        /// <summary>
        /// Shorthand name 
        /// </summary>
        string Code { get; }
        string[] Tags { get; }
    }
}
