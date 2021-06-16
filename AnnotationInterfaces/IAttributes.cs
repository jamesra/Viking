using System.Collections.Generic;

namespace Viking.AnnotationServiceTypes.Interfaces
{
    public interface IAttributes
    {
        IDictionary<string, object> Tags { get; }
    }
}
