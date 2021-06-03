namespace UnitsAndScale
{
    public interface IAxisUnits
    {
        /// <summary>
        /// Size of a single unit along the axis
        /// </summary>
        double Value { get; }

        /// <summary>
        /// Units of measurement that Value represents
        /// </summary>
        string Units { get; }
    }
}
