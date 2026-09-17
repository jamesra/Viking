using Viking.Common;

namespace Viking.UI.WPF.PropertyPages
{
    internal sealed class PropertyPageHost(IPropertyPageView view, PropertyPageAttribute metadata)
    {
        public IPropertyPageView View { get; } = view;

        public PropertyPageAttribute Metadata { get; } = metadata;
    }
}

