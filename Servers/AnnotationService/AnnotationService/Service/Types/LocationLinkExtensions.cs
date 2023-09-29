namespace AnnotationService.Types
{
    public static class LocationLinkExtensions
    {
        public static LocationLink Create(this ConnectomeDataModel.LocationLink link)
        {
            LocationLink ll = new LocationLink
            {
                SourceID = link.A,
                TargetID = link.B
            };
            return ll;
        }
    }
}
