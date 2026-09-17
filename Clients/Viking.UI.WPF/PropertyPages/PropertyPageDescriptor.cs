using System;
using Viking.Common;

namespace Viking.UI.WPF.PropertyPages
{
    internal sealed class PropertyPageDescriptor(Type pageType, PropertyPageAttribute attribute)
    {
        public Type PageType { get; } = pageType ?? throw new ArgumentNullException(nameof(pageType));

        public PropertyPageAttribute Attribute { get; } = attribute ?? throw new ArgumentNullException(nameof(attribute));

        public int Priority => Attribute.Priority;

        public Type TargetType => Attribute.ResolveTargetType();
    }
}

