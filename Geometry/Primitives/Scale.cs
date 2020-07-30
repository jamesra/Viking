﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Geometry
{ 
    /// <summary>
    /// Describes scale along an axis
    /// </summary>
    [Serializable]
    public class AxisUnits
    {
        public double Value { get; private set; }
        public string Units { get; private set; }

        public AxisUnits(double value, string units)
        {
            Value = value;
            Units = units; 
        }
    }

    /// <summary>
    /// Describes the scale for each axis in a 3D scene
    /// </summary>
    [Serializable]
    public class Scale
    {
        public AxisUnits X { get; private set; }
        public AxisUnits Y { get; private set; }
        public AxisUnits Z { get; private set; }

        public Scale(AxisUnits X, AxisUnits Y, AxisUnits Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z; 
        }
    }
}
