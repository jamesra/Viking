namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class AxisUnits
    {
        public static implicit operator AxisUnits(global::UnitsAndScale.AxisUnits src)
        {
            return new AxisUnits
            {
                Units = src.Units,
                Value = src.Value
            };
        }

        public static implicit operator global::UnitsAndScale.AxisUnits(AxisUnits src)
        {
            return new global::UnitsAndScale.AxisUnits(src.Value, src.Units);
        } 
    }
}

