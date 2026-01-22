namespace ColladaIO
{
    public static class Extensions
    {
        public static fx_common_float_or_param_typeFloat ToColladaFloat(this double value)
        {
            fx_common_float_or_param_typeFloat reflectivityValue = new()
            {
                Value = value
            };
            return reflectivityValue;
        }
    }
}
