using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebAnnotationModel
{
    /// <summary>
    /// This interface generates arbitrary keys 
    /// </summary>
    public interface IKeyGenerator<T>
    {
        T NextKey(); 
    }
}
