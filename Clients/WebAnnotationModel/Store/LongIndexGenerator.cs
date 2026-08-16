namespace WebAnnotationModel
{
    /// <summary>
    /// Generates a key for classes using a long as a key
    /// </summary>
    public class LongIndexGenerator : IKeyGenerator<long>
    {
        static long nextID = -1;

        public LongIndexGenerator()
        { }

        long IKeyGenerator<long>.NextKey()
        {
            return nextID--;
        }
    }
}
