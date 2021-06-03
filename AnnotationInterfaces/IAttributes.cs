using System.Collections.Generic;

namespace Annotation.Interfaces
{
    public interface IAttributes
    {
        IDictionary<string, object> Tags { get; }
    }
}
