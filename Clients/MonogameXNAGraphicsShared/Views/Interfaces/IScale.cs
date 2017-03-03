using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VikingXNAGraphics
{
    /// <summary>
    /// An interface for views that can be scaled by a constant amount
    /// </summary>
    public interface IScale
    {
        float Scale { get; set; }
    }
}
