namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class Scale
    {
        public static implicit operator Scale(global::UnitsAndScale.Scale src)
        {
            return new Scale
            {
                X = new AxisUnits { Units = src.X.Units, Value = src.X.Value },
                Y = new AxisUnits { Units = src.Y.Units, Value = src.Y.Value },
                Z = new AxisUnits { Units = src.Z.Units, Value = src.Z.Value },
            };
        } 
         
        public static implicit operator global::UnitsAndScale.Scale(Scale src)
        {
            return new global::UnitsAndScale.Scale((UnitsAndScale.AxisUnits)src.X, (UnitsAndScale.AxisUnits)src.Y, (UnitsAndScale.AxisUnits)src.Z);
        }
    }
}

