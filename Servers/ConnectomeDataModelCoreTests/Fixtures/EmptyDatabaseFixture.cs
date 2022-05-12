namespace Viking.DataModel.Annotation.Tests
{
    public class EmptyDatabaseFixture
    {
        public readonly AnnotationContext DataContext;

        public EmptyDatabaseFixture(IContextBuilder<AnnotationContext> dbContextBuilder)
        {
            DataContext = dbContextBuilder.DataContext;
        }
    }
}