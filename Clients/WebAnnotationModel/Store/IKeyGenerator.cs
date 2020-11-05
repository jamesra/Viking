namespace WebAnnotationModel
{
    /// <summary>
    /// This interface generates arbitrary keys 
    /// </summary>
    public interface IKeyGenerator<T>
    {
        T NextKey();
    }
}
