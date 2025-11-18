using Viking.Common;

namespace Viking.UI.WPF.PropertyPages
{
    internal sealed class PropertyPageHost
    {
        public PropertyPageHost(IPropertyPageView view, PropertyPageAttribute metadata)
        {
            View = view;
            Metadata = metadata;
        }

        public IPropertyPageView View { get; }

        public PropertyPageAttribute Metadata { get; }
    }
}

