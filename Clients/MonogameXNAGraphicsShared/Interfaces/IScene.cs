using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace VikingXNA
{
    public interface IScene
    {
        Matrix Projection { get; }
        Matrix World { get; }
        Matrix View { get; }

        Matrix ViewProj { get; }

        Matrix WorldViewProj { get; }
    }

    interface ICamera
    {
        Matrix View { get; }
    }
}
