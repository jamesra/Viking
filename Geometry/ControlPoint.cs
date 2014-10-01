using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Util
{
    class ControlPoint
    {
        public Vector2 Control;
        public Vector2 Mapped;

        public ControlPoint(Vector2 control, Vector2 mapped)
        {
            this.Control = control;
            this.Mapped = mapped; 
        }
    }
}
