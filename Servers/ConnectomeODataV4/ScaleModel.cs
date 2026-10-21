namespace ConnectomeODataV4
{
    /// <summary>
    /// OData-facing scale for one axis. The EDM complex type is built on this concrete class rather than
    /// <see cref="UnitsAndScale.IAxisUnits"/>: ODataLib rejects a resource whose runtime type (AxisUnits) differs
    /// from the interface-typed property type, which made GET /OData/Scale answer HTTP 500.
    /// </summary>
    public class AxisUnits
    {
        public double Value { get; set; }
        public string Units { get; set; }

        public static AxisUnits From(UnitsAndScale.IAxisUnits source) =>
            new AxisUnits { Value = source.Value, Units = source.Units };
    }

    /// <summary>OData-facing scale: X, Y and Z, each with Value and Units (shape unchanged).</summary>
    public class Scale
    {
        public AxisUnits X { get; set; }
        public AxisUnits Y { get; set; }
        public AxisUnits Z { get; set; }

        public static Scale From(UnitsAndScale.IScale source) =>
            new Scale { X = AxisUnits.From(source.X), Y = AxisUnits.From(source.Y), Z = AxisUnits.From(source.Z) };
    }
}
