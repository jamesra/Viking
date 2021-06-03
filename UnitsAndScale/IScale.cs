namespace UnitsAndScale
{
    public interface IScale
    {
        IAxisUnits X { get; }
        IAxisUnits Y { get; }
        IAxisUnits Z { get; }
    }
}
